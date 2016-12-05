﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using SystemInteract;
using DynamicExpresso;
using IPTables.Net;
using IPTables.Net.Iptables;
using IPTables.Net.Iptables.Helpers;
using IPTables.Net.Iptables.NativeLibrary;
using YFW.Net.Firewall;
using YFW.Net.Firewall.Dicts;
using YFW.Net.StringFormatter;

namespace YFW.Net
{
    public class RuleBuilder
    {
        private Dictionary<String, object> _mappings = new Dictionary<string, object>();
        private HashSet<IpTablesChain> _dynamicChainsCreated = new HashSet<IpTablesChain>(); 
        private DynamicObject _formatDb;
        private ISystemFactory _system;
        private string _nfbpf;
        private DynamicChainRegister _dcr;
        private Dictionary<int, IpTablesRuleSet> _ruleSets; 
        private string _tableState;
        private int _versionState;
        private Interpreter _interpreter;
        private string _currentArg;

        public RuleBuilder(IpTablesSystem system, String nfbpf, Dictionary<int, IpTablesRuleSet> ruleSets, FunctionRegistry functions = null)
        {
            if (functions == null)
            {
                functions = new FunctionRegistry();
            }
            _system = system.System;
            _nfbpf = nfbpf;
            var chainsDict =
                ruleSets.Select((a) => new KeyValuePair<int, IpTablesChainSet>(a.Key, a.Value.Chains))
                    .ToDictionary((a) => a.Key, (a) => a.Value);
            _dcr = new DynamicChainRegister(system, chainsDict);
            _formatDb = new DynamicDictionary<object>(_mappings);
            _ruleSets = ruleSets;
            _interpreter = new Interpreter();
            _interpreter.SetVariable("var", _mappings);
            functions.LoadFunctions(_interpreter);
        }

        public DynamicChainRegister Dcr
        {
            get { return _dcr; }
        }
 
        private string DynamicLookup(string dynamicName, string subname)
        {
            if (_tableState == null)
            {
                throw new Exception("Unexpected state");
            }

            var chain = Dcr.GetByVariable(dynamicName, _tableState, _versionState);
            if (chain == null)
            {
                throw new Exception("Variable " + dynamicName + " not found");
            }
            Debug.Assert(Dcr.IsDynamic(chain));
            var chainName = String.Format(chain.Name, subname);

            var createdChain = new IpTablesChain(chain.Table, chainName, chain.IpVersion, null);

            if (_dynamicChainsCreated.Contains(createdChain))
            {
                return chainName;
            }

            var ruleset = _ruleSets[chain.IpVersion];

            //Get chain rules, for all applicable tables and versions
            var rules = Dcr.GetDynamicChainRules(chain, subname);
            foreach (var r in rules)
            {
                ruleset.AddRule(r);
            }
            _dynamicChainsCreated.Add(createdChain);

            return chainName;
        }

        public String Format(String template, String table = null, int version = 4)
        {
            _tableState = table;
            _versionState = version;
            try
            {
                return HaackFormatter.HaackFormat(template, _formatDb);
            }
            catch (FormatException ex)
            {
                throw new FormatException(ex.Message + " Template: \"" + template + "\".", ex);
            }
        }

        public String ExecuteBash(String code)
        {
            String error;
            return ExecuteBash(code, out error);
        }

        public String ExecuteBash(String code, out String error)
        {
            var process = _system.StartProcess("bash", "-");
            process.StandardInput.WriteLine(code);
            process.StandardInput.Close();
            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            error = process.StandardError.ReadToEnd().TrimEnd(new char[]{'\n'});

            return output.TrimEnd(new char[]{'\n'});
        }

        public string DefineMapping(String name, String value, String @default = "")
        {
            value = value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                value = @default;
            }
            _mappings.Add(name, value);
            return value;
        }

        public string CompileBpf(string dltName, string code)
        {
            String error;
            return CompileBpf(dltName, code, out error);
        }

        public string CompileBpf(string dltName, string code, out String error)
        {
            if (IptcInterface.DllExists())
            {
                String ret = IptcInterface.BpfCompile(dltName, code, 2048 + code.Length*10);
                if (!String.IsNullOrEmpty(ret))
                {
                    error = null;
                    return ret;
                }
            }
            return ExecuteBash(_nfbpf + " " + dltName + " " + ShellHelper.EscapeArguments(code), out error);
        }

        public void DefineDynamicChain(string name)
        {
            _mappings.Add(name, new DynamicDictionaryCallback((a)=>DynamicLookup(name,a)));
        }

        private Dictionary<String, Lambda> _conditionalCache = new Dictionary<string, Lambda>();
        public bool IsConditionTrue(String condition)
        {
            if (String.IsNullOrWhiteSpace(condition))
            {
                return true;
            }
            try
            {
                Lambda lambda;
                lock (_conditionalCache)
                {
                    if (!_conditionalCache.TryGetValue(condition, out lambda))
                    {
                        lambda = _interpreter.Parse(condition);
                        _conditionalCache.Add(condition, lambda);
                    }
                }
                return (bool)lambda.Invoke();
            }
            catch (Exception ex)
            {
                throw new Exception(String.Format("An exception occured while evaluating condition: {0}",condition),ex);
            }
        }
    }
}
