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
			Repository.Save(solution.ToString(), bobSession);
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

			Repository.Save(request.UnsignedVoucher.ToString(), aliceSession);
			return Json(pubKey);
		}

		[HttpPost("api/v1/tumblers/0/channels")]
		public IActionResult OpenChannel(BobEscrowInformation request)
		{
			var height = Services.BlockExplorerService.GetCurrentHeight();
			var session = Repository.GetBobSession(request.ToString());
			if(session == null)
				return NotFound("channel-not-found");
			if(!session.GetCycle().IsInPhase(CyclePhase.TumblerChannelEstablishment, height))
				return BadRequest("incorrect-phase");
			var fee = Services.FeeService.GetFeeRate();
			try
			{
				var tumblerKeys = session.ReceiveBobEscrowInformation(request);
				var tx = Services.WalletService.FundTransaction(session.BuildEscrowTxOut(), fee);
				if(tx == null)
					return BadRequest("tumbler-insufficient-funds");
				Services.BroadcastService.Broadcast(tx);
				return this.Json(tumblerKeys);
			}
			catch(PuzzleException)
			{
				return BadRequest("incorrect-voucher");
			}
		}
	}
}
