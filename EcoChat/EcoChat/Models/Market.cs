using EcoChat.Enums;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcoChat.Models
{
	[BsonIgnoreExtraElements]
	public class Market
	{
		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
		public Dictionary<Resource, decimal> DefaultResourcePrices = new Dictionary<Resource, decimal>();
		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
		public Dictionary<Resource, decimal> ResourceBalance = new Dictionary<Resource, decimal>();
		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
		public Dictionary<Guid, Contract> Contracts = new Dictionary<Guid, Contract>();

		public Market()
		{
			DefaultResourcePrices.Add(Resource.Electricity, .5m);
			DefaultResourcePrices.Add(Resource.Water, 0.07m);
			DefaultResourcePrices.Add(Resource.Oil, 4);
			DefaultResourcePrices.Add(Resource.Wood, 5);
			DefaultResourcePrices.Add(Resource.Iron, 150);
			DefaultResourcePrices.Add(Resource.Machinery, 10000);
			DefaultResourcePrices.Add(Resource.Coal, 10);
			DefaultResourcePrices.Add(Resource.Cattle, 300);
			DefaultResourcePrices.Add(Resource.Chemicals, 2);
			DefaultResourcePrices.Add(Resource.Leather, 60);
			DefaultResourcePrices.Add(Resource.Minerals, 3);
			DefaultResourcePrices.Add(Resource.Paper, 12);
			DefaultResourcePrices.Add(Resource.Woodchips, 3);
			DefaultResourcePrices.Add(Resource.Steel, 1000);
			DefaultResourcePrices.Add(Resource.Seeds, 1);
			DefaultResourcePrices.Add(Resource.Chicken, 75);
			DefaultResourcePrices.Add(Resource.Eggs, 5);
			DefaultResourcePrices.Add(Resource.Wheat, 3);
			DefaultResourcePrices.Add(Resource.Apples, 12);
			DefaultResourcePrices.Add(Resource.Bananas, 15);
			DefaultResourcePrices.Add(Resource.Pears, 10);

			ResourceBalance.Add(Resource.Electricity, 0);
			ResourceBalance.Add(Resource.Water, 0);
			ResourceBalance.Add(Resource.Oil, 0);
			ResourceBalance.Add(Resource.Wood, 0);
			ResourceBalance.Add(Resource.Iron, 0);
			ResourceBalance.Add(Resource.Machinery, 0);
			ResourceBalance.Add(Resource.Coal, 0);
			ResourceBalance.Add(Resource.Cattle, 0);
			ResourceBalance.Add(Resource.Chemicals, 0);
			ResourceBalance.Add(Resource.Leather, 0);
			ResourceBalance.Add(Resource.Minerals, 0);
			ResourceBalance.Add(Resource.Paper, 0);
			ResourceBalance.Add(Resource.Woodchips, 0);
			ResourceBalance.Add(Resource.Steel, 0);
			ResourceBalance.Add(Resource.Seeds, 0);
			ResourceBalance.Add(Resource.Chicken, 0);
			ResourceBalance.Add(Resource.Eggs, 0);
			ResourceBalance.Add(Resource.Wheat, 0);
			ResourceBalance.Add(Resource.Apples, 0);
			ResourceBalance.Add(Resource.Bananas, 0);
			ResourceBalance.Add(Resource.Pears, 0);
		}

		public decimal Sigmoid(decimal value)
		{
			return 1.0m / (1.0m + (decimal)Math.Exp((double)-value));
		}
		static decimal Lerp(decimal firstFloat, decimal secondFloat, decimal by)
		{
			return firstFloat * (1.0m - by) + secondFloat * by;
		}

		public decimal GetPrice(Resource res)
		{
			decimal basePrice = DefaultResourcePrices[res];
			decimal balance = ResourceBalance[res];
			decimal price = 0;

			decimal effect = 0.5m;
			if (balance != 0)
			{
				effect = Sigmoid(balance / (1000000 / basePrice));
			}

			price = basePrice * (0.5m + effect);

			//if (balance != 0)
			//	price += balance / (100000 / basePrice);

			/*if (balance < 0)
				price = Lerp(basePrice, basePrice * 1000000000000, effect);
			else if (balance > 0)
				price = Lerp(basePrice, 0, effect);
			else
				price = basePrice;
				*/
			//DefaultResourcePrices[res] = price;

			return Math.Round(price, 2);
			//return price;
		}

		public string BalanceReport()
		{
			StringBuilder sb = new StringBuilder();
			foreach (Resource res in Enum.GetValues(typeof(Resource)))
			{
				decimal basePrice = DefaultResourcePrices[res];
				decimal balance = ResourceBalance[res];

				decimal effect = 0.5m;
				if (balance != 0)
				{
					effect = Sigmoid(balance / (1000000 / basePrice));
				}
				effect += 0.5m;

				sb.AppendLine($"*{res}*");
				sb.AppendLine($"`{Math.Round(-balance, 2)}` | `{Math.Round(balance / (1000000 / basePrice), 2)}`% | `{Math.Round(effect * 100, 2)}`%");
			}
			return sb.ToString();
		}

		public void Update(Random rng)
		{
			/*List<Resource> keys = new List<Resource>(ResourcePrices.Keys);
			foreach (Resource key in keys)
			{
				decimal roll = (decimal)rng.NextDouble() * 0.5m + 0.5m;
				if (key == Resource.Electricity)
					roll = (decimal)rng.NextDouble() * 0.25m + 0.75m;
				decimal value = DefaultResourcePrices[key] * roll;
				decimal rngRoll = (decimal)Math.Pow(rng.NextDouble(), 3);
				value = Math.Round(Lerp(ResourcePrices[key], value, rngRoll), 2);
				ResourcePrices[key] = value;
			}*/
			foreach (Resource res in Enum.GetValues(typeof(Resource)))
			{
				try
				{
					decimal change = 0;
					decimal balance = ResourceBalance[res];

					if (balance > 0)
						change = -1.0m / GetPrice(res) * 1000 * (decimal)rng.NextDouble();
					else if (balance < 0)
						change = 1.0m / GetPrice(res) * 1000 * (decimal)rng.NextDouble();

					ResourceBalance[res] += change; //+ ((decimal)rng.NextDouble() - 0.5m) * 125;
				}
				catch (Exception e)
				{

					throw e;
				}
			}
		}


	}
}
