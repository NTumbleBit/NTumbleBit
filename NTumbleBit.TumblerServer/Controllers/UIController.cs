using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.TumblerServer.Services;
using NTumbleBit.PuzzlePromise;
using NBitcoin;
using NTumbleBit.PuzzleSolver;
using static NTumbleBit.TumblerServer.Services.RPCServices.RPCBroadcastService;
using Microsoft.AspNetCore.Cors;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace NTumbleBit.TumblerServer.Controllers
{
	[EnableCors("AllowAnyOrigin")]
	public class UIController : Controller
	{
		public UIController(TumblerConfiguration configuration,
							ClassicTumblerRepository repo,
							ClassicTumblerParameters parameters,
							ExternalServices services,
							ClassicTumblerState state)
		{
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));
			if (parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			if (services == null)
				throw new ArgumentNullException(nameof(services));
			if (repo == null)
				throw new ArgumentNullException(nameof(repo));
			if (state == null)
				throw new ArgumentNullException(nameof(state));
			_Tumbler = configuration;
			_Repository = repo;
			_Parameters = parameters;
			_Services = services;
			_State = state;
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

		private readonly ClassicTumblerState _State;
		public ClassicTumblerState State
		{
			get
			{
				return _State;
			}
		}

		[HttpGet("api/Denomination")]
		public Money GetDenomination()
		{
			return Parameters.Denomination;
		}

		[HttpGet("api/Fee")]
		public Money GetFee()
		{
			return Parameters.Fee;
		}

		[HttpGet("api/BlockHeight")]
		public int GetBlockHeight()
		{
			return Services.BlockExplorerService.GetCurrentHeight();
		}

		[HttpGet("api/Tumbles")]
		public List<ClassicTumble> GetCycleStates()
		{
			return State.Tumbles;
		}

		[HttpGet("api/SolverServerSessionStates")]
		public Dictionary<string, List<SolverServerSession.State>> GetSolverServerSessionStates()
		{
			Dictionary<string, List<SolverServerSession.State>> states = new Dictionary<string, List<SolverServerSession.State>> { };
			IRepository repo = Repository.Repository;
			List<string> keys = repo.ListPartitionKeys().Where(s => s.Contains("Cycle")).ToList();
			foreach (var key in keys)
			{
				states.Add(key, repo.List<SolverServerSession.State>(key));
			}
			return states;
		}

		[HttpGet("api/PromiseServerSessionStates")]
		public Dictionary<string, List<PromiseServerSession.State>> GetPromiseServerSessionStates()
		{
			Dictionary<string, List<PromiseServerSession.State>> states = new Dictionary<string, List<PromiseServerSession.State>> { };
			IRepository repo = Repository.Repository;
			List<string> keys = repo.ListPartitionKeys().Where(s => s.Contains("Cycle")).ToList();
			foreach (var key in keys)
			{
				states.Add(key, repo.List<PromiseServerSession.State>(key));
			}
			return states;
		}
	}
}
