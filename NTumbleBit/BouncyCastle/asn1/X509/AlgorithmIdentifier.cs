using System;

namespace NTumbleBit.BouncyCastle.Asn1.X509
{
	class AlgorithmIdentifier
		: Asn1Encodable
	{
		private readonly DerObjectIdentifier objectID;
		private readonly Asn1Encodable parameters;
		private readonly bool parametersDefined;

		public AlgorithmIdentifier(
			DerObjectIdentifier objectID)
		{
			this.objectID = objectID;
		}

		public AlgorithmIdentifier(
			string objectID)
		{
			this.objectID = new DerObjectIdentifier(objectID);
		}

		public AlgorithmIdentifier(
			DerObjectIdentifier objectID,
			Asn1Encodable parameters)
		{
			this.objectID = objectID;
			this.parameters = parameters;
			this.parametersDefined = true;
		}

		public virtual DerObjectIdentifier ObjectID
		{
			get
			{
				return objectID;
			}
		}

		public Asn1Encodable Parameters
		{
			get
			{
				return parameters;
			}
		}

		/**
         * Produce an object suitable for an Asn1OutputStream.
         * <pre>
         *      AlgorithmIdentifier ::= Sequence {
         *                            algorithm OBJECT IDENTIFIER,
         *                            parameters ANY DEFINED BY algorithm OPTIONAL }
         * </pre>
         */
		public override Asn1Object ToAsn1Object()
		{
			Asn1EncodableVector v = new Asn1EncodableVector(objectID);

			if(parametersDefined)
			{
				if(parameters != null)
				{
					v.Add(parameters);
				}
				else
				{
					v.Add(DerNull.Instance);
				}
			}

			return new DerSequence(v);
		}
	}
}