using EcoChat.Enums;
using EcoChat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ResourceCollection = System.Collections.Generic.Dictionary<EcoChat.Enums.Resource, decimal>;

namespace EcoChat.Services
{
	public class BuildingService
	{
		public static Building CreateBuilding(Company company, BuildingType type)
		{
			return new Building
			{
				Type = type,
				Operational = true,
				Tier = 1
			};
		}

		public static IEnumerable<ProductionDefinition> GetBuildingProductionDefs(BuildingType bType)
		{
			List<ProductionDefinition> defs = new List<ProductionDefinition>();
			switch (bType)
			{
				case BuildingType.PowerPlant:
					defs.Add(GetProductionDefinition(Resource.Electricity));
					break;
				case BuildingType.Well:
					defs.Add(GetProductionDefinition(Resource.Water));
					defs.Add(GetProductionDefinition(Resource.Oil));
					break;
				case BuildingType.Mine:
					defs.Add(GetProductionDefinition(Resource.Iron));
					defs.Add(GetProductionDefinition(Resource.Coal));
					defs.Add(GetProductionDefinition(Resource.Minerals));
					break;
				case BuildingType.Plantation:
					defs.Add(GetProductionDefinition(Resource.Wood));
					defs.Add(GetProductionDefinition(Resource.Seeds));
					defs.Add(GetProductionDefinition(Resource.Wheat));
					defs.Add(GetProductionDefinition(Resource.Apples));
					defs.Add(GetProductionDefinition(Resource.Bananas));
					defs.Add(GetProductionDefinition(Resource.Pears));
					break;
				case BuildingType.Husbandry:
					defs.Add(GetProductionDefinition(Resource.Cattle));
					defs.Add(GetProductionDefinition(Resource.Chicken));
					defs.Add(GetProductionDefinition(Resource.Eggs));
					break;
				case BuildingType.Factory:
					defs.Add(GetProductionDefinition(Resource.Machinery));
					defs.Add(GetProductionDefinition(Resource.Woodchips));
					defs.Add(GetProductionDefinition(Resource.Chemicals));
					defs.Add(GetProductionDefinition(Resource.Paper));
					defs.Add(GetProductionDefinition(Resource.Leather));
					defs.Add(GetProductionDefinition(Resource.Steel));
					break;
				default:
					break;
			}
			return defs;
		}

		public static ProductionDefinition GetProductionDefinition(Resource type)
		{
			switch (type)
			{
				case Resource.Water:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 125}
						},
						Output = new ResourceCollection
						{
							{type, 1000}
						},
						Upkeep = 0
					};
				case Resource.Electricity:
					return new ProductionDefinition
					{
						Output = new ResourceCollection
						{
							{type, 100}
						},
						Upkeep = 0
					};
				case Resource.Oil:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 15},
							{Resource.Water, 100},
						},
						Output = new ResourceCollection
						{
							{type, 25}
						},
						Upkeep = 0
					};
				case Resource.Wood:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Water, 250},
						},
						Output = new ResourceCollection
						{
							{type, 15}
						},
						Upkeep = 0
					};
				case Resource.Iron:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 500},
							{Resource.Water, 350},
						},
						Output = new ResourceCollection
						{
							{type, 3}
						},
						Upkeep = 0
					};
				case Resource.Machinery:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Iron, 5},
							{Resource.Electricity, 200},
							{Resource.Oil, 10 },
						},
						Output = new ResourceCollection
						{
							{type, 0.1m}
						},
						Upkeep = 0
					};
				case Resource.Coal:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 50},
							{Resource.Water, 150 },
						},
						Output = new ResourceCollection
						{
							{type, 10}
						},
						Upkeep = 0
					};
				case Resource.Woodchips:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 25},
							{Resource.Wood, 45 },
						},
						Output = new ResourceCollection
						{
							{type, 100}
						},
						Upkeep = 0
					};
				case Resource.Minerals:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 100},
							{Resource.Water, 50 },
						},
						Output = new ResourceCollection
						{
							{type, 25}
						},
						Upkeep = 0
					};
				case Resource.Chemicals:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 25},
							{Resource.Water, 100 },
							{Resource.Minerals, 10}
						},
						Output = new ResourceCollection
						{
							{type, 100}
						},
						Upkeep = 0
					};
				case Resource.Paper:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Electricity, 25},
							{Resource.Water, 15 },
							{Resource.Chemicals, 12},
							{Resource.Woodchips, 100}
						},
						Output = new ResourceCollection
						{
							{type, 32}
						},
						Upkeep = 0
					};
				case Resource.Cattle:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Water, 350},
							{Resource.Wheat, 25},
						},
						Output = new ResourceCollection
						{
							{type, 1}
						},
						Upkeep = 0
					};
				case Resource.Leather:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Water, 75},
							{Resource.Cattle, 1},
						},
						Output = new ResourceCollection
						{
							{type, 5}
						},
						Upkeep = 0
					};
				case Resource.Steel:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Iron, 0.05m},
							{Resource.Coal, 3},
							{Resource.Chemicals, 5},
							{Resource.Electricity, 50},
							{Resource.Water, 50},
						},
						Output = new ResourceCollection
						{
							{type, 0.05m}
						},
						Upkeep = 0
					};
				case Resource.Seeds:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Water, 50},
						},
						Output = new ResourceCollection
						{
							{type, 100}
						},
						Upkeep = 0
					};
				case Resource.Eggs:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Seeds, 75},
							{Resource.Water, 75},
						},
						Output = new ResourceCollection
						{
							{type, 30}
						},
						Upkeep = 0
					};
				case Resource.Chicken:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Seeds, 100},
							{Resource.Water, 100},
						},
						Output = new ResourceCollection
						{
							{type, 3}
						},
						Upkeep = 0
					};
				case Resource.Wheat:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Seeds, 25},
							{Resource.Water, 75},
						},
						Output = new ResourceCollection
						{
							{type, 40}
						},
						Upkeep = 0
					};
				case Resource.Apples:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Seeds, 25},
							{Resource.Water, 250},
						},
						Output = new ResourceCollection
						{
							{type, 10}
						},
						Upkeep = 0
					};
				case Resource.Bananas:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Seeds, 25},
							{Resource.Water, 300},
						},
						Output = new ResourceCollection
						{
							{type, 10}
						},
						Upkeep = 0
					};
				case Resource.Pears:
					return new ProductionDefinition
					{
						Input = new ResourceCollection
						{
							{Resource.Seeds, 25},
							{Resource.Water, 200},
						},
						Output = new ResourceCollection
						{
							{type, 10}
						},
						Upkeep = 0
					};
				default:
					return null;
			}
		}

		public static ResourceCollection QuoteBuilding(Company company, BuildingType type)
		{
			return new ResourceCollection {
				{ Resource.Iron, 100 },
				{ Resource.Wood, 250 },
			};
		}

		/*public static ResourceCollection QuoteBuilding(Company company, Resource type)
		{
			Building existing = company.Buildings.Values.FirstOrDefault(o => o.Production.Output.First().Key == type);
			if (existing != null)
				return new ResourceCollection {
				{ Resource.Steel, (decimal)Math.Pow(existing.Tier, 3)},
			};
			else
				return new ResourceCollection {
				{ Resource.Iron, 100 },
				{ Resource.Wood, 250 },
			};
		}*/
	}
}
