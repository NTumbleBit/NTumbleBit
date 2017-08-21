using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using NTumbleBit.ClassicTumbler;
using System.Net;
using NBitcoin;
using NTumbleBit.Services;
using NBitcoin.Crypto;
using NTumbleBit.Logging;
using Microsoft.Extensions.Logging;
using NTumbleBit.ClassicTumbler.Server.Models;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace NTumbleBit.ClassicTumbler.Server.Controllers
{
	public class MainController : Controller
	{
		public MainController(TumblerRuntime runtime)
		{
			if(runtime == null)
				throw new ArgumentNullException(nameof(runtime));
			_Runtime = runtime;
			_Repository = new ClassicTumblerRepository(_Runtime);
		}



		private readonly TumblerRuntime _Runtime;
		public TumblerRuntime Runtime
		{
			get
			{
				return _Runtime;
			}
		}
		public Tracker Tracker
		{
			get
			{
				return _Runtime.Tracker;
			}
		}

		public ExternalServices Services
		{
			get
			{
				return _Runtime.Services;
			}
		}

		ClassicTumblerRepository _Repository;
		public ClassicTumblerRepository Repository
		{
			get
			{
				return _Repository;
			}
		}


		public ClassicTumblerParameters Parameters
		{
			get
			{
				return _Runtime.ClassicTumblerParameters;
			}
		}

		[HttpGet("api/v1/tumblers/{tumblerId}/parameters")]
		public ClassicTumblerParameters GetParameters(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			return tumblerId;
		}

		[HttpGet("api/v1/tumblers/{tumblerId}/vouchers")]
		public UnsignedVoucherInformation AskUnsignedVoucher(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var cycleParameters = Parameters.CycleGenerator.GetRegistratingCycle(height);
			PuzzleSolution solution = null;
			var puzzle = Parameters.VoucherKey.PublicKey.GeneratePuzzle(ref solution);
			uint160 nonce;
			var cycle = cycleParameters.Start;
			var signature = Runtime.VoucherKey.Sign(NBitcoin.Utils.ToBytes((uint)cycle, true), out nonce);
			return new UnsignedVoucherInformation
			{
				CycleStart = cycle,
				Nonce = nonce,
				Puzzle = puzzle.PuzzleValue,
				EncryptedSignature = new XORKey(solution).XOR(signature)
			};
		}


		[HttpGet("api/v1/tumblers/{tumblerId}/clientchannels/{cycleStart}")]
		public TumblerEscrowKeyResponse RequestTumblerEscrowKey(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleStart)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var cycle = GetCycle(cycleStart);
			int keyIndex;
			var key = Repository.GetNextKey(cycle.Start, out keyIndex);
			if(!cycle.IsInPhase(CyclePhase.ClientChannelEstablishment, height))
				throw new ActionResultException(BadRequest("invalid-phase"));
			return new TumblerEscrowKeyResponse { PubKey = key.PubKey, KeyIndex = keyIndex };
		}

		private CycleParameters GetCycle(int cycleStart)
		{
			try
			{
				return Parameters.CycleGenerator.GetCycle(cycleStart);
			}
			catch(InvalidOperationException)
			{
				Logs.Tumbler.LogDebug($"Invalid cycle received {cycleStart}");
				throw new ActionResultException(BadRequest("invalid-cycle"));
			}
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/clientchannels/confirm")]
		public PuzzleSolution SignVoucher(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			[FromBody]SignVoucherRequest request)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			if(request.UnsignedVoucher == null)
				throw new ActionResultException(BadRequest("Missing UnsignedVoucher"));
			if(request.MerkleProof == null)
				throw new ActionResultException(BadRequest("Missing MerkleProof"));
			if(request.Transaction == null)
				throw new ActionResultException(BadRequest("Missing Transaction"));
			if(request.ClientEscrowKey == null)
				throw new ActionResultException(BadRequest("Missing ClientEscrowKey"));

			var cycle = GetCycle(request.Cycle);
			var height = Services.BlockExplorerService.GetCurrentHeight();
			if(!cycle.IsInPhase(CyclePhase.ClientChannelEstablishment, height))
			{
				throw new ActionResultException(BadRequest("invalid-phase"));
			}

			if(request.MerkleProof.PartialMerkleTree
				.GetMatchedTransactions()
				.FirstOrDefault() != request.Transaction.GetHash() || !request.MerkleProof.Header.CheckProofOfWork())
			{
				Logs.Tumbler.LogDebug("Invalid transaction merkle proof");
				throw new ActionResultException(BadRequest("invalid-merkleproof"));
			}

			var confirmations = Services.BlockExplorerService.GetBlockConfirmations(request.MerkleProof.Header.GetHash());
			if((confirmations < Parameters.CycleGenerator.FirstCycle.SafetyPeriodDuration))
			{
				Logs.Tumbler.LogDebug("Not enough confirmations");
				throw new ActionResultException(BadRequest("not-enough-confirmation"));
			}

			var transaction = request.Transaction;
			if(transaction.Outputs.Count > 2)
			{
				Logs.Tumbler.LogDebug("Incorrect number of outputs");
				throw new ActionResultException(BadRequest("invalid-transaction"));
			}


			var key = Repository.GetKey(cycle.Start, request.KeyReference);

			var expectedEscrow = new EscrowScriptPubKeyParameters(request.ClientEscrowKey, key.PubKey, cycle.GetClientLockTime());

			var expectedTxOut = new TxOut(Parameters.Denomination + Parameters.Fee, expectedEscrow.ToScript().Hash);
			var escrowedCoin =
				transaction
				.Outputs
				.AsCoins()
				.Where(c => c.TxOut.Value == expectedTxOut.Value
							&& c.TxOut.ScriptPubKey == expectedTxOut.ScriptPubKey)
				.Select(c => c.ToScriptCoin(expectedEscrow.ToScript()))
				.FirstOrDefault();

			if(escrowedCoin == null)
			{
				Logs.Tumbler.LogDebug("Could not find escrowed coin");
				throw new ActionResultException(BadRequest("invalid-transaction"));
			}

			try
			{
				var solverServerSession = new SolverServerSession(Runtime.TumblerKey, Parameters.CreateSolverParamaters());
				solverServerSession.ConfigureEscrowedCoin(escrowedCoin, key);

				Services.BlockExplorerService.Track(escrowedCoin.ScriptPubKey);
				if(!Services.BlockExplorerService.TrackPrunedTransaction(request.Transaction, request.MerkleProof))
					throw new ActionResultException(BadRequest("invalid-merkleproof"));

				if(!Repository.MarkUsedNonce(cycle.Start, new uint160(key.PubKey.Hash.ToBytes())))
				{
					Logs.Tumbler.LogDebug("Nonce already marked");
					throw new ActionResultException(BadRequest("invalid-transaction"));
				}
				Repository.Save(cycle.Start, solverServerSession);
				Logs.Tumbler.LogInformation($"Cycle {cycle.Start} Proof of Escrow signed for " + transaction.GetHash());

				var correlation = GetCorrelation(solverServerSession);
				Tracker.AddressCreated(cycle.Start, TransactionType.ClientEscrow, escrowedCoin.ScriptPubKey, correlation);
				Tracker.TransactionCreated(cycle.Start, TransactionType.ClientEscrow, request.Transaction.GetHash(), correlation);
				var solution = request.UnsignedVoucher.WithRsaKey(Runtime.VoucherKey.PubKey).Solve(Runtime.VoucherKey);
				return solution;
			}
			catch(PuzzleException ex)
			{
				Logs.Tumbler.LogDebug(new EventId(), ex, "Puzzle failed");
				throw new ActionResultException(BadRequest("invalid-transaction"));
			}
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/channels")]
		public async Task<ScriptCoinModel> OpenChannel(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			[FromBody] OpenChannelRequest request)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var cycle = GetCycle(request.CycleStart);
			if(!cycle.IsInPhase(CyclePhase.TumblerChannelEstablishment, height))
				throw new ActionResultException(BadRequest("invalid-phase"));
			var fee = await Services.FeeService.GetFeeRateAsync();
			try
			{
				if(!Parameters.VoucherKey.PublicKey.Verify(request.Signature, NBitcoin.Utils.ToBytes((uint)request.CycleStart, true), request.Nonce))
					throw new ActionResultException(BadRequest("incorrect-voucher"));
				if(!Repository.MarkUsedNonce(request.CycleStart, request.Nonce))
				{
					throw new ActionResultException(BadRequest("nonce-already-used"));
				}

				var escrowKey = new Key();

				var escrow = new EscrowScriptPubKeyParameters();
				escrow.LockTime = cycle.GetTumblerLockTime();
				escrow.Receiver = request.EscrowKey;
				escrow.Initiator = escrowKey.PubKey;

				Logs.Tumbler.LogInformation($"Cycle {cycle.Start} Asked to open channel");
				var txOut = new TxOut(Parameters.Denomination, escrow.ToScript().Hash);
				var tx = await Services.WalletService.FundTransactionAsync(txOut, fee);
				var correlation = escrow.GetCorrelation();
				var escrowTumblerLabel = $"Cycle {cycle.Start} Tumbler Escrow";
				Services.BlockExplorerService.Track(txOut.ScriptPubKey);

				Tracker.AddressCreated(cycle.Start, TransactionType.TumblerEscrow, txOut.ScriptPubKey, correlation);
				Tracker.TransactionCreated(cycle.Start, TransactionType.TumblerEscrow, tx.GetHash(), correlation);
				var bobCount = Parameters.CountEscrows(tx, Client.Identity.Bob);
				Logs.Tumbler.LogInformation($"Cycle {cycle.Start} channel created {tx.GetHash()} with {bobCount} users");

				var coin = tx.Outputs.AsCoins().First(o => o.ScriptPubKey == txOut.ScriptPubKey && o.TxOut.Value == txOut.Value);

				var session = new PromiseServerSession(Parameters.CreatePromiseParamaters());
				var redeem = Services.WalletService.GenerateAddress();
				session.ConfigureEscrowedCoin(coin.ToScriptCoin(escrow.ToScript()), escrowKey, redeem.ScriptPubKey);
				Repository.Save(cycle.Start, session);

				Services.BroadcastService.Broadcast(tx);

				var redeemTx = session.CreateRedeemTransaction(fee);
				Tracker.AddressCreated(cycle.Start, TransactionType.TumblerRedeem, redeem.ScriptPubKey, correlation);
				Services.TrustedBroadcastService.Broadcast(cycle.Start, TransactionType.TumblerRedeem, correlation, redeemTx);
				return new ScriptCoinModel(session.EscrowedCoin);
			}
			catch(PuzzleException)
			{
				throw new ActionResultException(BadRequest("incorrect-voucher"));
			}
			catch(NotEnoughFundsException ex)
			{
				Logs.Tumbler.LogInformation(ex.Message);
				throw new ActionResultException(BadRequest("tumbler-insufficient-funds"));
			}
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/channels/{cycleId}/{channelId}/signhashes")]
		public PuzzlePromise.ServerCommitment[] SignHashes(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleId, string channelId, [FromBody]SignaturesRequest sigReq)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var session = GetPromiseServerSession(cycleId, channelId, CyclePhase.TumblerChannelEstablishment);
			var hashes = session.SignHashes(sigReq);
			Repository.Save(cycleId, session);
			return hashes;
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/channels/{cycleId}/{channelId}/checkrevelation")]
		public ServerCommitmentsProof CheckRevelation(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleId, string channelId, [FromBody]PuzzlePromise.ClientRevelation revelation)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var session = GetPromiseServerSession(cycleId, channelId, CyclePhase.TumblerChannelEstablishment);
			var proof = session.CheckRevelation(revelation);
			Repository.Save(cycleId, session);
			return proof;
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
			CycleParameters cycle = GetCycle(cycleId);
			if(!cycle.IsInPhase(expectedPhase, height))
				throw BadRequest("invalid-phase").AsException();
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/clientchannels/{cycleId}/{channelId}/solvepuzzles")]
		public PuzzleSolver.ServerCommitment[] SolvePuzzles(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleId, string channelId, [FromBody]PuzzleValue[] puzzles)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.PaymentPhase);
			var commitments = session.SolvePuzzles(puzzles);
			Repository.Save(cycleId, session);
			return commitments;
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/clientschannels/{cycleId}/{channelId}/checkrevelation")]
		public SolutionKey[] CheckRevelation(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleId, string channelId, [FromBody]PuzzleSolver.ClientRevelation revelation)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.PaymentPhase);
			var solutions = session.CheckRevelation(revelation);
			Repository.Save(cycleId, session);
			return solutions;
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/clientschannels/{cycleId}/{channelId}/checkblindfactors")]
		public async Task<OfferInformation> CheckBlindFactors(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleId, string channelId, [FromBody]BlindFactor[] blindFactors)
		{
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.PaymentPhase);
			var feeRate = await Services.FeeService.GetFeeRateAsync();
			var fulfillKey = session.CheckBlindedFactors(blindFactors, feeRate);
			Repository.Save(cycleId, session);
			return fulfillKey;
		}

		[HttpPost("api/v1/tumblers/{tumblerId}/clientchannels/{cycleId}/{channelId}/offer")]
		public SolutionKey[] FulfillOffer(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleId, string channelId, [FromBody]SignatureWrapper wrapper)
		{
			var signature = wrapper?.Signature;
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			if(signature == null)
				throw new ActionResultException(BadRequest("Missing Signature"));
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.TumblerCashoutPhase);
			var feeRate = Services.FeeService.GetFeeRateAsync().GetAwaiter().GetResult();
			if(session.Status != SolverServerStates.WaitingFulfillment)
				throw new ActionResultException(BadRequest("invalid-state"));
			try
			{
				var cycle = GetCycle(cycleId);
				var cashout = Services.WalletService.GenerateAddress();

				var fulfill = session.FulfillOffer(signature, cashout.ScriptPubKey, feeRate);
				fulfill.BroadcastAt = new LockTime(cycle.GetPeriods().Payment.End - 1);
				Repository.Save(cycle.Start, session);

				var signedOffer = session.GetSignedOfferTransaction();
				signedOffer.BroadcastAt = fulfill.BroadcastAt - 1;
				var correlation = GetCorrelation(session);

				var offerScriptPubKey = session.GetInternalState().OfferCoin.ScriptPubKey;
				Services.BlockExplorerService.Track(offerScriptPubKey);

				Tracker.AddressCreated(cycle.Start, TransactionType.ClientOffer, offerScriptPubKey, correlation);
				Services.TrustedBroadcastService.Broadcast(cycle.Start, TransactionType.ClientOffer, correlation, signedOffer);

				Tracker.AddressCreated(cycle.Start, TransactionType.ClientFulfill, cashout.ScriptPubKey, correlation);

				if(!Runtime.NoFulFill)
				{
					Services.TrustedBroadcastService.Broadcast(cycle.Start, TransactionType.ClientFulfill, correlation, fulfill);
				}
				return Runtime.Cooperative ? session.GetSolutionKeys() : new SolutionKey[0];
			}
			catch(PuzzleException ex)
			{
				throw new ActionResultException(BadRequest(ex.Message));
			}
		}

		private static CorrelationId GetCorrelation(SolverServerSession session)
		{
			return EscrowScriptPubKeyParameters.GetFromCoin(session.EscrowedCoin).GetCorrelation();
		}


		[HttpPost("api/v1/tumblers/{tumblerId}/clientchannels/{cycleId}/{channelId}/escape")]
		public async Task<NoData> GiveEscapeKey(
			[ModelBinder(BinderType = typeof(TumblerParametersModelBinder))]
			ClassicTumblerParameters tumblerId,
			int cycleId, string channelId, [FromBody]SignatureWrapper wrapper)
		{
			var clientSignature = wrapper?.Signature;
			if(tumblerId == null)
				throw new ArgumentNullException("tumblerId");
			var session = GetSolverServerSession(cycleId, channelId, CyclePhase.TumblerCashoutPhase);
			if(session.Status != SolverServerStates.WaitingEscape)
				throw new ActionResultException(BadRequest("invalid-state"));

			var fee = await Services.FeeService.GetFeeRateAsync();
			try
			{
				var dummy = new Key().PubKey.Hash.ScriptPubKey;
				var tx = session.GetSignedEscapeTransaction(clientSignature, fee, dummy);
				var state = session.GetInternalState();

				// The previous tx is broadcastable, but let's give change to the wallet to join everything in a single transaction
				tx = await Runtime.Services.WalletService.ReceiveAsync(state.EscrowedCoin, clientSignature, state.EscrowKey, fee);

				Logs.Tumbler.LogInformation($"Cashing out from {tx.Inputs.Count} Alices");

				var correlation = GetCorrelation(session);
				Tracker.AddressCreated(cycleId, TransactionType.ClientEscape, tx.Outputs[0].ScriptPubKey, correlation);
				Tracker.TransactionCreated(cycleId, TransactionType.ClientEscape, tx.GetHash(), correlation);

				Services.BroadcastService.Broadcast(tx);
			}
			catch(PuzzleException ex)
			{
				throw new ActionResultException(BadRequest(ex.Message));
			}
			return new NoData();
		}
	}
}
