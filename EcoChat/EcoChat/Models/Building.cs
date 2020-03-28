using EcoChat.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcoChat.Models
{
	public class Building
	{
		public int Tier { get; set; } = 1;
		public bool Operational = true;
		public BuildingType Type { get; set; }
		public ProductionDefinition Production { get; set; }

		public void Cycle(Company company)
		{
			if (Production == null)
			{
				company.CycleReport.AppendLine($"⚠ {Type} is unconfigured.");
				return;
			}
			if(!Operational)
			{
				company.CycleReport.AppendLine($"{Type}(T{Tier}) is turned off");
				return;
			}
			bool hasResources = true;
			if (Production.Input != null)
			{
				foreach (var cost in Production.Input)
				{
					decimal costValue = cost.Value * Tier;
					if (!company.Warehouse.ContainsKey(cost.Key))
					{
						company.CycleReport.AppendLine($"{Type} could not produce '{Production.Output.First().Key}': not enough '{cost.Key}({costValue})' to produce.");
						hasResources = false;
					}
					else if (company.Warehouse[cost.Key] < costValue)
					{
						company.CycleReport.AppendLine($"{Type} could not produce '{Production.Output.First().Key}': not enough '{cost.Key}({costValue})' to produce.");
						hasResources = false;
					}
				}
			}

			if (hasResources)
			{
				if (Production.Input != null)
				{
					foreach (var input in Production.Input)
					{
						decimal inputValue = input.Value * Tier;
						company.Warehouse[input.Key] -= inputValue;
					}
				}
				foreach (var output in Production.Output)
				{
					decimal outputValue = output.Value * Tier;

					if (!company.Warehouse.ContainsKey(output.Key))
						company.Warehouse.Add(output.Key, outputValue);
					else
						company.Warehouse[output.Key] += outputValue;

					//company.CycleReport.AppendLine($"{Type}(T{Tier}) produced {output.Key} `{outputValue}`");
				}
			}
		}
	}
}
