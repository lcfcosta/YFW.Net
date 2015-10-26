﻿using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace YFW.Net.Models
{
    public class IpTablesDetails
    {
        private List<IpSetDetails> _sets = new List<IpSetDetails>();
        private List<ChainDetails> _chains = new List<ChainDetails>();
        private List<RuleDetails> _rules = new List<RuleDetails>();

        [YamlMember(Alias="chains")]
        public List<ChainDetails> Chains
        {
            get { return _chains; }
            set { _chains = value; }
        }

        [YamlMember(Alias = "rules")]
        public List<RuleDetails> Rules
        {
            get { return _rules; }
            set { _rules = value; }
        }

        [YamlMember(Alias = "ipset")]
        public List<IpSetDetails> Sets
        {
            get { return _sets; }
            set { _sets = value; }
        }
    }
}
