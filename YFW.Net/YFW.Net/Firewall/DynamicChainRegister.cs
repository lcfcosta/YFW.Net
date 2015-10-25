﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using IPTables.Net;
using IPTables.Net.Iptables;

namespace YFW.Net.Firewall
{
    public class DynamicChainRegister
    {
        private Dictionary<Tuple<String, String, int>, IpTablesChain> _variables = new Dictionary<Tuple<String, String, int>, IpTablesChain>();
        private Dictionary<IpTablesChain, List<IpTablesRule>> _dynamicChains = new Dictionary<IpTablesChain, List<IpTablesRule>>();
        private IpTablesSystem _system;
        private IpTablesChainSet _chains4;
        private IpTablesChainSet _chains6;

        public DynamicChainRegister(IpTablesSystem system, IpTablesChainSet chainsSet4, IpTablesChainSet chainSet6)
        {
            _system = system;
            _chains4 = chainsSet4;
            _chains6 = chainSet6;
        }

        public void RegisterDynamicChain(String variable, String table, String chainName, int ipVersion)
        {
            var regChain = new IpTablesChain(table, chainName, ipVersion, _system);
            if (_dynamicChains.ContainsKey(regChain))
            {
                throw new Exception(String.Format("A chain of ipv{0},{1}:{2} is already registered", ipVersion, chainName, table));
            }
            _dynamicChains.Add(regChain, new List<IpTablesRule>());

            _variables.Add(new Tuple<string, string, int>(table, variable, ipVersion), regChain);//todo: Support for multiple table!
        }

        public IpTablesChain GetByVariable(String var, String table, int version)
        {
            var tup = new Tuple<string, string, int>(table, var, version);
            if (!_variables.ContainsKey(tup))
            {
                return null;
            }

            return _variables[tup];
        }

        public void AddRule(IpTablesRule rule)
        {
            Debug.Assert(IsDynamic(rule));
            _dynamicChains[rule.Chain].Add(rule);
        }

        public void FeedRule(IpTablesRule rule)
        {
            if (IsDynamic(rule))
            {
                AddRule(rule);
            }
        }

        public List<IpTablesRule> GetDynamicChainRules(IpTablesChain chain, String arg)
        {
            if (!IsDynamic(chain))
            {
                throw new Exception("Chain should be dynamic");
            }

            var chains = chain.IpVersion == 4 ? _chains4 : _chains6;

            List<IpTablesRule> rules = new List<IpTablesRule>();
            foreach (var rule in _dynamicChains[chain])
            {
                var formatted = String.Format(rule.GetActionCommand(), arg);
                var newRule = IpTablesRule.Parse(formatted, _system, chains, rule.Chain.IpVersion,
                    rule.Chain.Table, IpTablesRule.ChainCreateMode.CreateNewChainIfNeeded);
                rules.Add(newRule);
            }
            return rules;
        }

        public bool IsDynamic(IpTablesChain chain)
        {
            var comparisonChain = new IpTablesChain(chain.Table, chain.Name, chain.IpVersion, _system);
            return _dynamicChains.ContainsKey(comparisonChain);
        }

        public bool IsDynamic(IpTablesRule rule)
        {
            return IsDynamic(rule.Chain);
        }
    }
}
