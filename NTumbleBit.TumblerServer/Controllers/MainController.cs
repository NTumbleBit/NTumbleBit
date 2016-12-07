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

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace NTumbleBit.TumblerServer.Controllers
{
	public class MainController : Controller
	{
		public MainController(TumblerConfiguration configuration, ClassicTumblerParameters parameters, ServerServices services)
		{
			if(configuration == null)
				throw new ArgumentNullException("configuration");
			if(parameters == null)
				throw new ArgumentNullException("parameters");
			if(services == null)
				throw new ArgumentNullException("services");

			_Tumbler = configuration;
			_Repository = configuration.Repository;
			_Parameters = parameters;
			_Services = services;
		}


		private readonly ServerServices _Services;
		public ServerServices Services
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
		public AskVoucherResponse AskVoucherParameters()
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var cycleParameters = Parameters.CycleGenerator.GetRegistratingCycle(height);
			var bobSession = new TumblerBobServerSession(Parameters, Tumbler.TumblerKey, Tumbler.VoucherKey, cycleParameters.Start);
			PuzzleSolution solution = null;
			var voucher = bobSession.GenerateUnsignedVoucher(ref solution);
			Repository.Save(GetKey(solution), bobSession);
			return new AskVoucherResponse()
			{
				Cycle = cycleParameters.Start,
				UnsignedVoucher = voucher
			};
		}


		[HttpPost("api/v1/tumblers/0/clientchannels")]
		public IActionResult RequestTumblerEscrowKey([FromBody]ClientEscrowInformation request)
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var aliceSession = new TumblerAliceServerSession(Parameters, Tumbler.TumblerKey, Tumbler.VoucherKey);
			var pubKey = aliceSession.ReceiveAliceEscrowInformation(request);
			if(!aliceSession.GetCycle().IsInPhase(CyclePhase.ClientChannelEstablishment, height))
				return BadRequest("incorrect-phase");
			Repository.Save(GetKey(aliceSession), aliceSession);
			Services.BlockExplorerService.Track(aliceSession.BuildEscrowTxOut().ScriptPubKey);
			return Json(pubKey);
		}

		private static string GetKey(TumblerAliceServerSession aliceSession)
		{
			return aliceSession.BuildEscrowTxOut().ScriptPubKey.ToHex();
		}

		[HttpPost("api/v1/tumblers/0/clientchannels/voucher")]
		public IActionResult SolveVoucher([FromBody]uint256 txId)
		{
			var transaction = Services.BlockExplorerService.GetTransaction(txId);
			if(transaction == null || 
				(transaction.Confirmations < Parameters.CycleGenerator.FirstCycle.SafetyPeriodDuration))
				return BadRequest("not-enough-confirmation");
			if(transaction.Transaction.Outputs.Count > 2)
				return BadRequest("invalid-transaction");

			var sessions = transaction
				.Transaction
				.Outputs
				.Select(o => Repository.GetAliceSession(o.ScriptPubKey.ToHex()))
				.Where(o => o != null)
				.ToList();
			if(sessions.Count != 1)
				return BadRequest("invalid-transaction");

			var height = Services.BlockExplorerService.GetCurrentHeight();
			if(!sessions[0].GetCycle().IsInPhase(CyclePhase.ClientChannelEstablishment, height))
				return BadRequest("incorrect-phase");


			var voucher = sessions[0].ConfirmAliceEscrow(transaction.Transaction);
			Repository.Save(GetKey(sessions[0]), sessions[0]);
			return Json(voucher);
		}

		[HttpPost("api/v1/tumblers/0/channels")]
		public IActionResult OpenChannel([FromBody] BobEscrowInformation request)
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var session = Repository.GetBobSession(GetKey(request.SignedVoucher));
			if(session == null)
				return NotFound("channel-not-found");
			if(!session.GetCycle().IsInPhase(CyclePhase.TumblerChannelEstablishment, height))
				return BadRequest("incorrect-phase");
			var fee = Services.FeeService.GetFeeRate();
			try
			{
				session.ReceiveBobEscrowInformation(request);
				var txOut = session.BuildEscrowTxOut();
				var tx = Services.WalletService.FundTransaction(txOut, fee);
				if(tx == null)
					return BadRequest("tumbler-insufficient-funds");
				Services.BlockExplorerService.Track(txOut.ScriptPubKey);
				Services.BroadcastService.Broadcast(tx);
				var escrowInfo = session.SetSignedTransaction(tx);
				Repository.Save(GetKey(request.SignedVoucher), session);
				return this.Json(escrowInfo);
			}
			catch(PuzzleException)
			{
				return BadRequest("incorrect-voucher");
			}
		}

		private string GetKey(PuzzleSolution signedVoucher)
		{
			return Hashes.Hash160(signedVoucher.ToBytes()).ToString();
		}
	}
}
