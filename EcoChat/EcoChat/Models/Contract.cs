using EcoChat.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcoChat.Models
{
	public class Contract
	{
		public Guid ID { get; set; }
		public int Length { get; set; }
		public Resource Resource { get; set; }
		public decimal Amount { get; set; }
		public decimal Price { get; set; }
		public int Sender { get; set; }
		public int Receiver { get; set; }

		public decimal Total
		{
			get
			{
				return Amount * Price;
			}
		}
	}
}
