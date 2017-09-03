using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit
{
	//<Bob.PubKey>
	//OP_DEPTH OP_3 OP_EQUAL
	//OP_IF

	//	OP_SWAP
	//	<Alice.PubKey> OP_CHECKSIGVERIFY OP_CODESEPARATOR
	//OP_ELSE
	//    0 OP_CLTV OP_DROP
	//OP_ENDIF
	//OP_CHECKSIG
	public class EscrowScriptPubKeyParameters
	{

		public EscrowScriptPubKeyParameters()
		{

		}

		public EscrowScriptPubKeyParameters(PubKey initiator, PubKey receiver, LockTime lockTime)
		{
			this.Initiator = initiator;
			this.Receiver = receiver;
			this.LockTime = lockTime;
		}
		public static EscrowScriptPubKeyParameters GetFromCoin(ScriptCoin coin)
		{
			return GetFromScript(coin.Redeem);
		}
		private static EscrowScriptPubKeyParameters GetFromScript(Script script)
		{
			EscrowScriptPubKeyParameters parameters = new EscrowScriptPubKeyParameters();
			try
			{
				var data = script.ToOps().Where(o => o.PushData != null).ToArray();
				parameters.Initiator = new PubKey(data[0].PushData);
				parameters.Receiver = new PubKey(data[2].PushData);
				parameters.LockTime = new LockTime(data[3].GetInt().Value);
				//Verify it is the same as if we had done it
				if(parameters.ToScript() != script)
					throw new Exception();
				return parameters;
			}
			catch { return null; }
		}
		public PubKey Initiator
		{
			get; set;
		}

		public PubKey Receiver
		{
			get; set;
		}
		public LockTime LockTime
		{
			get; set;
		}
		
		public Script ToScript()
		{
			if(Initiator == null || Receiver == null || LockTime == default(LockTime))
				throw new InvalidOperationException("Parameters are incomplete");
			EscrowScriptPubKeyParameters parameters = new EscrowScriptPubKeyParameters();
			List<Op> ops = new List<Op>();
			ops.Add(Op.GetPushOp(Initiator.ToBytes()));
			ops.Add(OpcodeType.OP_DEPTH);
			ops.Add(OpcodeType.OP_3);
			ops.Add(OpcodeType.OP_EQUAL);
			ops.Add(OpcodeType.OP_IF);
			{
				ops.Add(OpcodeType.OP_SWAP);
				ops.Add(Op.GetPushOp(Receiver.ToBytes()));
				ops.Add(OpcodeType.OP_CHECKSIGVERIFY);
				ops.Add(OpcodeType.OP_CODESEPARATOR);
			}
			ops.Add(OpcodeType.OP_ELSE);
			{
				ops.Add(Op.GetPushOp(LockTime));
				ops.Add(OpcodeType.OP_DROP);
			}
			ops.Add(OpcodeType.OP_ENDIF);
			ops.Add(OpcodeType.OP_CHECKSIG);
			return new Script(ops.ToArray());
		}

		public Script GetInitiatorScriptCode()
		{
			List<Op> ops = new List<Op>();
			ops.Add(OpcodeType.OP_ELSE);
			{
				ops.Add(Op.GetPushOp(LockTime));
				ops.Add(OpcodeType.OP_DROP);
			}
			ops.Add(OpcodeType.OP_ENDIF);
			ops.Add(OpcodeType.OP_CHECKSIG);
			return new Script(ops.ToArray());
		}

		public override bool Equals(object obj)
		{
			EscrowScriptPubKeyParameters item = obj as EscrowScriptPubKeyParameters;
			if(item == null)
				return false;
			return ToScript().Equals(item.ToScript());
		}
		public static bool operator ==(EscrowScriptPubKeyParameters a, EscrowScriptPubKeyParameters b)
		{
			if(System.Object.ReferenceEquals(a, b))
				return true;
			if(((object)a == null) || ((object)b == null))
				return false;
			return a.ToScript() == b.ToScript();
		}

		public static bool operator !=(EscrowScriptPubKeyParameters a, EscrowScriptPubKeyParameters b)
		{
			return !(a == b);
		}

		public override int GetHashCode()
		{
			return ToScript().GetHashCode();
		}
	}
}
