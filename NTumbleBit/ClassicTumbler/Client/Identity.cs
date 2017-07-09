using System;
using System.Collections.Generic;
using System.Text;

namespace NTumbleBit.ClassicTumbler.Client
{
    public class Identity : IEquatable<Identity>
    {
		private Role _role { get; }
		private int _cycle { get; }

		private bool _doesntMatter { get; }
		public static Identity DoesntMatter => new Identity(doesntMatter: true);

		public Identity(Role role, int cycle)
		{
			_role = role;
			_cycle = cycle;
			_doesntMatter = false;
		}
		
		/// <param name="doesntMatter">Only accept true</param>
		private Identity(bool doesntMatter)
		{
			if (!doesntMatter) throw new ArgumentException(nameof(doesntMatter));
			_doesntMatter = doesntMatter;

			// dummy
			_role = Role.Alice;
			_cycle = -1;
		}

		public override string ToString()
		{
			if (_doesntMatter) return "'Does not matter'";
			else return $"{_role} {_cycle}";
		}

		#region Equality
		public static bool operator ==(Identity a1, Identity a2) => 
			a1._doesntMatter
			|| a2._doesntMatter
			|| (a1._role == a2._role && a1._cycle == a2._cycle);
		public static bool operator !=(Identity a1, Identity a2) => !(a1 == a2);
		public override bool Equals(object obj) => obj is Identity && this == (Identity)obj;
		public bool Equals(Identity other) => this == other;
		public override int GetHashCode() => 
			_doesntMatter 
			? _doesntMatter.GetHashCode()
			: _role.GetHashCode() ^ _cycle.GetHashCode();		
		#endregion
	}
	public enum Role
	{
		Alice,
		Bob
	}
}
