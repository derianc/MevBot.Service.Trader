using MevBot.Service.Data;
using NRules;
using NRules.Fluent;
using System.Reflection;

namespace MevBot.Service.Trader.rules
{
    public class RuleEngine
    {
        public void EvaluateTrade(TradeData tradeData)
        {
            // Create a repository and load all rules from the current assembly.
            var repository = new RuleRepository();

            // Loads all rules dynamically
            repository.Load(x => x.From(Assembly.GetExecutingAssembly()));

            // Compile the rules.
            var compiler = new RuleCompiler();
            var factory = compiler.Compile(repository.GetRules());
            var session = factory.CreateSession();

            // Insert the tradeData as a fact.
            session.Insert(tradeData);

            // Fire the rules.
            session.Fire();
        }
    }
}
