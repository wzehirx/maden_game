using System;

namespace Kazi.Core
{
    /// <summary>Bakiye. ASLA negatif olamaz.</summary>
    public sealed class Wallet
    {
        private long balance;
        public long Balance { get { return balance; } set { balance = value < 0 ? 0 : value; } }

        public void Add(long amount)
        {
            if (amount <= 0) return;
            long nb = balance + amount;
            balance = nb < balance ? long.MaxValue : nb; // taşmaya karşı
        }

        /// <summary>Yetiyorsa harcar ve true döner; yetmiyorsa hiçbir şey yapmaz.</summary>
        public bool TrySpend(long amount)
        {
            if (amount < 0) return false;
            if (amount > balance) return false;
            balance -= amount;
            return true;
        }

        /// <summary>En fazla bakiye kadar düşer (ceza için). Gerçekten düşülen miktarı döner.</summary>
        public long SpendUpTo(long amount)
        {
            if (amount <= 0) return 0;
            long take = Math.Min(amount, balance);
            balance -= take;
            return take;
        }
    }
}
