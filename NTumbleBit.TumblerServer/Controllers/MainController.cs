using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;

// For more information on enabling Web API for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace NTumbleBit.TumblerServer.Controllers
{
	public class MainController : Controller
	{
		public MainController(TumblerConfiguration configuration)
		{
			if(configuration == null)
				throw new ArgumentNullException("configuration");
			_Tumbler = configuration;
		}


		private readonly TumblerConfiguration _Tumbler;
		public TumblerConfiguration Tumbler
		{
			get
			{
				return _Tumbler;
			}
		}
		
		[HttpGet("api/v1/tumbler/promise/parameters")]
		public PromiseParameters GetPromiseParameters()
		{
			return new PromiseParameters(Tumbler.RsaKey.PubKey);
		}

		[HttpGet("api/v1/tumbler/solver/parameters")]
		public SolverParameters GetSolverParameters()
		{
			return new SolverParameters(Tumbler.RsaKey.PubKey);
		}
	}
}
