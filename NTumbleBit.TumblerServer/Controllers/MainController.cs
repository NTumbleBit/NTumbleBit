using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.TumblerServer.Models;
using System.Net;
using NBitcoin;
using NTumbleBit.TumblerServer.Services;
using NBitcoin.Crypto;
using NTumbleBit.Common.Logging;
using Microsoft.Extensions.Logging;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace NTumbleBit.TumblerServer.Controllers
{
	public class MainController : Controller
	{
		public MainController(TumblerConfiguration configuration, ClassicTumblerRepository repo, ClassicTumblerParameters parameters, ExternalServices services)
		{
			if(configuration == null)
				throw new ArgumentNullException(nameof(configuration));
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			if(services == null)
				throw new ArgumentNullException(nameof(services));
			if(repo == null)
				throw new ArgumentNullException(nameof(repo));
			_Tumbler = configuration;
			_Repository = repo;
			_Parameters = parameters;
			_Services = services;
		}


		private readonly ExternalServices _Services;
		public ExternalServices Services
		{
			get
			{
				return _Services;
			}
		}

		private readonly ClassicTumblerRepository _Repository;
		public ClassicTumblerRepository Repository
		{
			get
			{
				return _Repository;
			}
		}

		private readonly TumblerConfiguration _Tumbler;
		public TumblerConfiguration Tumbler
		{
			get
			{
				return _Tumbler;
			}
		}


		private readonly ClassicTumblerParameters _Parameters;
		public ClassicTumblerParameters Parameters
		{
			get
			{
				return _Parameters;
			}
		}

		[HttpGet("api/v1/tumblers/0/parameters")]
		public ClassicTumblerParameters GetSolverParameters()
		{
			return Parameters;
		}

		[HttpGet("api/v1/tumblers/0/vouchers")]
		public UnsignedVoucherInformation AskUnsignedVoucher()
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var cycleParameters = Parameters.CycleGenerator.GetRegistratingCycle(height);
			BobServerChannelNegotiation session = CreateBobServerChannelNegotiation(cycleParameters.Start);
			return session.GenerateUnsignedVoucher();
		}


		[HttpPost("api/v1/tumblers/0/clientchannels")]
		public IActionResult RequestTumblerEscrowKey([FromBody]int cycleStart)
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var cycle = Parameters.CycleGenerator.GetCycle(cycleStart);
			int keyIndex;
			var key = Repository.GetNextKey(cycle.Start, out keyIndex);
			if(!cycle.IsInPhase(CyclePhase.ClientChannelEstablishment, height))
				return BadRequest("incorrect-phase");
			return Json(new TumblerEscrowKeyResponse { PubKey = key.PubKey, KeyIndex = keyIndex });
		}

		[HttpPost("api/v1/tumblers/0/clientchannels/confirm")]
		public IActionResult SignVoucher([FromBody]SignVoucherRequest request)
		{
			if(request.MerkleProof.PartialMerkleTree
				.GetMatchedTransactions()
				.FirstOrDefault() != request.Transaction.GetHash() || !request.MerkleProof.Header.CheckProofOfWork())
				return BadRequest("invalid-merkleproof");


			var transaction = request.Transaction;
			if(transaction.Outputs.Count > 2)
				return BadRequest("invalid-transaction");

			var cycle = Parameters.CycleGenerator.GetCycle(request.ClientEscrowInformation.Cycle);
			var height = Services.BlockExplorerService.GetCurrentHeight();
			if(!cycle.IsInPhase(CyclePhase.ClientChannelEstablishment, height))
				return BadRequest("incorrect-phase");

			AliceServerChannelNegotiation aliceNegotiation = new AliceServerChannelNegotiation(Parameters, Tumbler.TumblerKey, Tumbler.VoucherKey);
			var key = Repository.GetKey(cycle.Start, request.KeyReference);
			if(key.PubKey != request.TumblerEscrowPubKey)
				return BadRequest("incorrect-escrowpubkey");

			aliceNegotiation.ReceiveClientEscrowInformation(request.ClientEscrowInformation, key);

			try
			{
				var expectedTxOut = aliceNegotiation.BuildEscrowTxOut();
				PuzzleSolution voucher;
				var solverServerSession = aliceNegotiation.ConfirmClientEscrow(transaction, out voucher);

				var confirmations = Services.BlockExplorerService.GetBlockConfirmations(request.MerkleProof.Header.GetHash());
				if((confirmations < Parameters.CycleGenerator.FirstCycle.SafetyPeriodDuration))
					return BadRequest("not-enough-confirmation");

				Services.BlockExplorerService.Track($"Cycle {cycle.Start} Client Escrow", expectedTxOut.ScriptPubKey);
				if(!Services.BlockExplorerService.TrackPrunedTransaction(request.Transaction, request.MerkleProof))
					return BadRequest("invalid-merkleproof");

				if(!Repository.MarkUsedNonce(cycle.Start, new uint160(key.PubKey.Hash.ToBytes())))
					return BadRequest("invalid-transaction");
				Repository.Save(cycle.Start, solverServerSession);
				Logs.Server.LogInformation($"Cycle {cycle.Start} Proof of Escrow signed for " + transaction.GetHash());
				return Json(voucher);
			}
			catch(PuzzleException)
			{
				return BadRequest("invalid-transaction");
			}
		}

		[HttpPost("api/v1/tumblers/0/channels")]
		public IActionResult OpenChannel([FromBody] OpenChannelRequest request)
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			BobServerChannelNegotiation session = CreateBobServerChannelNegotiation(request.CycleStart);
			var cycle = session.GetCycle();
			if(!cycle.IsInPhase(CyclePhase.TumblerChannelEstablishment, height))
				return BadRequest("incorrect-phase");
			var fee = Services.FeeService.GetFeeRate();
			try
			{
				session.ReceiveBobEscrowInformation(request);
				if(!Repository.MarkUsedNonce(request.CycleStart, request.Nonce))
				{
					return BadRequest("nonce-already-used");
				}
				Logs.Server.LogInformation($"Cycle {cycle.Start} Asked to open channel");
				var txOut = session.BuildEscrowTxOut();
				var tx = Services.WalletService.FundTransaction(txOut, fee);
				if(tx == null)
				{
					Logs.Server.LogInformation("Not funds left for creating a channel");
					return BadRequest("tumbler-insufficient-funds");
				}

				var escrowTumblerLabel = $"Cycle {cycle.Start} Tumbler Escrow";
				Services.BlockExplorerService.Track(escrowTumblerLabel, txOut.ScriptPubKey);
				Services.BroadcastService.Broadcast(escrowTumblerLabel, tx);
				Logs.Server.LogInformation($"Cycle {cycle.Start} Channel created " + tx.GetHash());
				var promiseServerSession = session.SetSignedTransaction(tx);
				Repository.Save(cycle.Start, promiseServerSession);

				var redeem = Services.WalletService.GenerateAddress($"Cycle {cycle.Start} Tumbler Redeem");
				var redeemTx = promiseServerSession.CreateRedeemTransaction(fee, redeem.ScriptPubKey);
				Services.TrustedBroadcastService.Broadcast($"Cycle {session.GetCycle().Start} Tumbler Redeem (locked until: {redeemTx.Transaction.LockTime})", redeemTx);
				return Json(promiseServerSession.EscrowedCoin);
			}
			catch(PuzzleException)
			{
				return BadRequest("incorrect-voucher");
			}
		}

		private BobServerChannelNegotiation CreateBobServerChannelNegotiation(int cycleStart)
		{
			return new BobServerChannelNegotiation(Parameters, Tumbler.TumblerKey, Tumbler.VoucherKey, cycleStart);
		}

		[HttpPost("api/v1/tumblers/0/channels/{cycleId}/{channelId}/signhashes")]
		public IActionResult SignHashes(int cycleId, string channelId, [FromBody]SignaturesRequest sigReq)
		{
			var session = GetPromiseServerSession(cycleId, channelId, CyclePhase.TumblerChannelEstablishment);
			var hashes = session.SignHashes(sigReq);
			Repository.Save(cycleId, session);
			return Json(hashes);
		}

		[HttpPost("api/v1/tumblers/0/channels/{cycleId}/{channelId}/checkrevelation")]
		public IActionResult CheckRevelation(int cycleId, string channelId, [FromBody]PuzzlePromise.ClientRevelation revelation)
		{
			var session = GetPromiseServerSession(cycleId, channelId, CyclePhase.TumblerChannelEstablishment);
			var proof = session.CheckRevelation(revelation);
			Repository.Save(cycleId, session);
			return Json(proof);
		}

		private PromiseServerSession GetPromiseServerSession(int cycleId, string channelId, CyclePhase expectedPhase)
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var session = Repository.GetPromiseServerSession(cycleId, channelId);
			if(session == null)
				throw NotFound("channel-not-found").AsException();
			CheckPhase(expectedPhase, height, cycleId);
			return session;
		}

		private SolverServerSession GetSolverServerSession(int cycleId, string channelId, CyclePhase expectedPhase)
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var session = Repository.GetSolverServerSession(cycleId, channelId);
			if(session == null)
				throw NotFound("channel-not-found").AsException();
			CheckPhase(expectedPhase, height, cycleId);
			return session;
		}

		private void CheckPhase(CyclePhase expectedPhase, int height, int cycleId)
		{
			CycleParameters cycle = Parameters.CycleGenerator.GetCycle(cycleId);
			if(!cycle.IsInPhase(expectedPhase, height))
				throw BadRequest("invalid-phase").AsException();
		}

		[HttpPost("api/v1/tumblers/0/clientchannels/{cycleId}/{channelId}/solvepuzzles")]
		public IActionResult SolvePuzzles(int cycleId, string channelId, [FromBody]PuzzleValue[] puzzles)
		{
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.PaymentPhase);
			var commitments = session.SolvePuzzles(puzzles);
			Repository.Save(cycleId, session);
			return Json(commitments);
		}

		[HttpPost("api/v1/tumblers/0/clientschannels/{cycleId}/{channelId}/checkrevelation")]
		public IActionResult CheckRevelation(int cycleId, string channelId, [FromBody]PuzzleSolver.ClientRevelation revelation)
		{
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.PaymentPhase);
			var solutions = session.CheckRevelation(revelation);
			Repository.Save(cycleId, session);
			return Json(solutions);
		}

		[HttpPost("api/v1/tumblers/0/clientschannels/{cycleId}/{channelId}/checkblindfactors")]
		public IActionResult CheckBlindFactors(int cycleId, string channelId, [FromBody]BlindFactor[] blindFactors)
		{
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.PaymentPhase);
			var feeRate = Services.FeeService.GetFeeRate();
			var fulfillKey = session.CheckBlindedFactors(blindFactors, feeRate);
			var cycle = Parameters.CycleGenerator.GetCycle(cycleId);
			//later we will track it for fulfillment
			Services.BlockExplorerService.Track($"Cycle {cycle.Start} Client Offer", session.GetOfferScriptPubKey());
			Repository.Save(cycleId, session);
			return Json(fulfillKey);
		}

		[HttpPost("api/v1/tumblers/0/clientchannels/{cycleId}/{channelId}/offer")]
		public IActionResult FulfillOffer(int cycleId, string channelId, [FromBody]TransactionSignature clientSignature)
		{
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.TumblerCashoutPhase);
			var feeRate = Services.FeeService.GetFeeRate();
			if(session.Status != SolverServerStates.WaitingFulfillment)
				return BadRequest("invalid-state");
			try
			{
				var cycle = Parameters.CycleGenerator.GetCycle(cycleId);
				var cashout = Services.WalletService.GenerateAddress($"Cycle {cycle.Start} Tumbler Cashout");
				var fulfill = session.FulfillOffer(clientSignature, cashout.ScriptPubKey, feeRate);
				fulfill.BroadcastAt = new LockTime(cycle.GetPeriods().Payment.End - 1);
				Repository.Save(cycle.Start, session);

				var signedOffer = session.GetSignedOfferTransaction();
				signedOffer.BroadcastAt = fulfill.BroadcastAt - 1;
				Services.TrustedBroadcastService.Broadcast($"Cycle {cycle.Start} Client Offer Transaction (planned for: {signedOffer.BroadcastAt})", signedOffer);
				Services.TrustedBroadcastService.Broadcast($"Cycle {cycle.Start} Tumbler Fulfillment Transaction (planned for: {fulfill.BroadcastAt})", fulfill);
				return Json(session.GetSolutionKeys());
			}
			catch(PuzzleException)
			{
				return BadRequest("invalid-offer");
			}
		}
	}
}
