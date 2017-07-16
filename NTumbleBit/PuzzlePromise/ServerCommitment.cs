using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.PuzzlePromise
{
    public class ServerCommitment : IBitcoinSerializable
    {
		public ServerCommitment()
		{

		}
		public ServerCommitment(PuzzleValue puzzleValue, byte[] promise)
		{
			Puzzle = puzzleValue;
			Promise = promise;
		}

		PuzzleValue _Puzzle;
		public PuzzleValue Puzzle
		{
			get
			{
				return _Puzzle;
			}
			set
			{
				_Puzzle = value;
			}
		}


		byte[] _Promise;
		public byte[] Promise
		{
			get
			{
				return _Promise;
			}
			set
			{
				_Promise = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _Puzzle);
			stream.ReadWriteAsVarString(ref _Promise);
		}
	}
}
