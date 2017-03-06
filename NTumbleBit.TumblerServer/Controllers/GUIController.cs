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
	public class GUIController : Controller
	{
		public GUIController(TumblerConfiguration configuration, ClassicTumblerRepository repo, ClassicTumblerParameters parameters, ExternalServices services)
		{
			if (configuration == null)
				throw new ArgumentNullException(nameof(configuration));
			if (parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			if (services == null)
				throw new ArgumentNullException(nameof(services));
			if (repo == null)
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

		[HttpGet("api/Cycles")]
		public List<ClassicTumblerCycle> GetCycleStates()
		{
			List<ClassicTumblerCycle> cycles = new List<ClassicTumblerCycle>();
			IRepository repo = Repository.Repository;
			List<string> keys = repo.ListPartitionKeys().Where(x => x.Contains("Cycle")).ToList();
			foreach (var key in keys)
			{
				cycles.Add(new ClassicTumblerCycle(
					key.Substring(key.LastIndexOf("_")+1),
					repo.List<SolverServerSession.State>(key),
					repo.List<PromiseServerSession.State>(key)
				));
			}
			return cycles;

		}

		[HttpGet("api/SolverServerSessionStates")]
		public Dictionary<string, SolverServerSession.State[]> GetSolverServerSessionStates()
		{
			Dictionary<string, SolverServerSession.State[]> states = new Dictionary<string, SolverServerSession.State[]> { };
			IRepository repo = Repository.Repository;
			List<string> keys = repo.ListPartitionKeys().Where(s => s.Contains("Cycle")).ToList();
			foreach (var key in keys)
			{
				states.Add(key, repo.List<SolverServerSession.State>(key));
			}
			return states;
		}

		[HttpGet("api/PromiseServerSessionStates")]
		public List<PromiseServerSession.State> GetPromiseServerSessionStates()
		{
			List<PromiseServerSession.State> states = new List<PromiseServerSession.State> { };
			IRepository repo = Repository.Repository;
			List<string> keys = repo.ListPartitionKeys().Where(s => s.Contains("Cycle")).ToList();
			foreach (var key in keys)
			{
				states.AddRange(repo.List<PromiseServerSession.State>(key));
			}
			return states;
		}
	}
}
