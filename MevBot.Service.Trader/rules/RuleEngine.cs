using System.Reflection;
using NRules;
using NRules.Fluent;
using NRules.Fluent.Dsl;
using MevBot.Service.Data;

namespace MevBot.Service.Trader.rules
{
    // Custom rule activator that uses DI to resolve rule instances.
    public class DiRuleActivator : IRuleActivator
    {
        private readonly IServiceProvider _serviceProvider;

        public DiRuleActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IEnumerable<Rule> Activate(Type ruleType)
        {
            // Attempt to resolve the rule from the DI container, otherwise instantiate it.
            var instance = _serviceProvider.GetService(ruleType) ?? Activator.CreateInstance(ruleType);

            if (instance is Rule ruleInstance)
                return new List<Rule> { ruleInstance };

            throw new InvalidOperationException($"Failed to activate rule type: {ruleType.FullName}");
        }
    }

    public class RuleEngine
    {
        private readonly IServiceProvider _serviceProvider;

        public RuleEngine(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void EvaluateTrade(TradeData tradeData)
        {
            // Create a repository and load all rules from the current assembly.
            var repository = new RuleRepository();
            repository.Activator = new DiRuleActivator(_serviceProvider); // Use DI for rule activation.
            repository.Load(x => x.From(Assembly.GetExecutingAssembly()));

            // Compile the rules.
            var compiler = new RuleCompiler();
            var factory = compiler.Compile(repository.GetRules()); // No need to pass an activator here.
            var session = factory.CreateSession();

            // Insert the trade data as a fact and fire the rules.
            session.Insert(tradeData);
            session.Fire();
        }
    }
}
