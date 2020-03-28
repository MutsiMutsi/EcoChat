using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcoChat.Models
{
	public class PowerGrid
	{
		public decimal PowerProduced { get; set; }
		public decimal PowerSold { get; set; }

		public Dictionary<int, decimal> DayProducers { get; set; } = new Dictionary<int, decimal>();
		public Dictionary<int, decimal> DayConsumers { get; set; } = new Dictionary<int, decimal>();

		public void AddProducer(int userid, decimal power)
		{
			if (DayProducers.ContainsKey(userid))
				DayProducers[userid] += power;
			else
				DayProducers.Add(userid, power);
			PowerProduced += power;
		}

		public void TakePower(int userid, decimal power)
		{
			if (DayConsumers.ContainsKey(userid))
				DayConsumers[userid] += power;
			else
				DayConsumers.Add(userid, power);
			PowerSold += power;
		}

		public Dictionary<int, decimal> GetDailyBalance()
		{
			Dictionary<int, decimal> payout = new Dictionary<int, decimal>();
			foreach (var producer in DayProducers)
			{
				decimal weight = PowerProduced / producer.Value;
				payout.Add(producer.Key, weight * PowerSold);
			}
			foreach (var consumer in DayConsumers)
			{
				if (payout.ContainsKey(consumer.Key))
					payout[consumer.Key] -= consumer.Value;
				else
					payout.Add(consumer.Key, -consumer.Value);
			}
			return payout;
		}
	}
}
