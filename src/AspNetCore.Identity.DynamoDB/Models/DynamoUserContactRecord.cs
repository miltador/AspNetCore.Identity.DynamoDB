using System;

namespace AspNetCore.Identity.DynamoDB.Models
{
    public abstract class DynamoUserContactRecord : IEquatable<DynamoUserEmail>
    {
        protected DynamoUserContactRecord() { }

        protected DynamoUserContactRecord(string value) : this()
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Value = value;
        }

        public string Value { get; set; }
        public DateTime ConfirmedOn { get; set; }

        public bool IsConfirmed()
        {
            return ConfirmedOn != default(DateTime);
        }

        public void SetConfirmed()
        {
            SetConfirmed(DateTime.Now);
        }

        public void SetConfirmed(DateTime confirmationTime)
        {
            if (ConfirmedOn == default(DateTime))
            {
                ConfirmedOn = confirmationTime;
            }
        }

        public void SetUnconfirmed()
        {
            ConfirmedOn = default(DateTime);
        }

        public bool Equals(DynamoUserEmail other)
        {
            return other != null && other.Value.Equals(Value);
        }
    }
}
