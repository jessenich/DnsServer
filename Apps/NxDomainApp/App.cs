﻿/*
Technitium DNS Server
Copyright (C) 2023  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using DnsServerCore.ApplicationCommon;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using TechnitiumLibrary;
using TechnitiumLibrary.Net.Dns;
using TechnitiumLibrary.Net.Dns.ResourceRecords;

namespace NxDomain
{
    public class App : IDnsApplication, IDnsAuthoritativeRequestHandler
    {
        #region variables

        DnsSOARecordData _soaRecord;

        bool _enableBlocking;
        bool _allowTxtBlockingReport;

        IReadOnlyDictionary<string, object> _blockListZone;

        #endregion

        #region IDisposable

        public void Dispose()
        {
            //do nothing
        }

        #endregion

        #region private

        private static string GetParentZone(string domain)
        {
            int i = domain.IndexOf('.');
            if (i > -1)
                return domain.Substring(i + 1);

            //dont return root zone
            return null;
        }

        private bool IsZoneBlocked(string domain, out string blockedDomain)
        {
            domain = domain.ToLower();

            do
            {
                if (_blockListZone.TryGetValue(domain, out _))
                {
                    //found zone blocked
                    blockedDomain = domain;
                    return true;
                }

                domain = GetParentZone(domain);
            }
            while (domain is not null);

            blockedDomain = null;
            return false;
        }

        #endregion

        #region public

        public Task InitializeAsync(IDnsServer dnsServer, string config)
        {
            _soaRecord = new DnsSOARecordData(dnsServer.ServerDomain, "hostadmin@" + dnsServer.ServerDomain, 1, 14400, 3600, 604800, 60);

            using JsonDocument jsonDocument = JsonDocument.Parse(config);
            JsonElement jsonConfig = jsonDocument.RootElement;

            _enableBlocking = jsonConfig.GetProperty("enableBlocking").GetBoolean();
            _allowTxtBlockingReport = jsonConfig.GetProperty("allowTxtBlockingReport").GetBoolean();
            _blockListZone = jsonConfig.ReadArrayAsMap("blocked", delegate (JsonElement jsonDomainName) { return new Tuple<string, object>(jsonDomainName.GetString(), null); });

            return Task.CompletedTask;
        }

        public Task<DnsDatagram> ProcessRequestAsync(DnsDatagram request, IPEndPoint remoteEP, DnsTransportProtocol protocol, bool isRecursionAllowed)
        {
            if (!_enableBlocking)
                return Task.FromResult<DnsDatagram>(null);

            DnsQuestionRecord question = request.Question[0];

            if (!IsZoneBlocked(question.Name, out string blockedDomain))
                return Task.FromResult<DnsDatagram>(null);

            if (_allowTxtBlockingReport && (question.Type == DnsResourceRecordType.TXT))
            {
                //return meta data
                DnsResourceRecord[] answer = new DnsResourceRecord[] { new DnsResourceRecord(question.Name, DnsResourceRecordType.TXT, question.Class, 60, new DnsTXTRecordData("source=nx-domain-app; domain=" + blockedDomain)) };

                return Task.FromResult(new DnsDatagram(request.Identifier, true, DnsOpcode.StandardQuery, false, false, request.RecursionDesired, false, false, false, DnsResponseCode.NoError, request.Question, answer) { Tag = DnsServerResponseType.Blocked });
            }
            else
            {
                string parentDomain = GetParentZone(blockedDomain);
                if (parentDomain is null)
                    parentDomain = string.Empty;

                IReadOnlyList<DnsResourceRecord> authority = new DnsResourceRecord[] { new DnsResourceRecord(parentDomain, DnsResourceRecordType.SOA, question.Class, 60, _soaRecord) };

                return Task.FromResult(new DnsDatagram(request.Identifier, true, DnsOpcode.StandardQuery, false, false, request.RecursionDesired, false, false, false, DnsResponseCode.NxDomain, request.Question, null, authority) { Tag = DnsServerResponseType.Blocked });
            }
        }

        #endregion

        #region properties

        public string Description
        { get { return "Blocks configured domain names with a NX Domain response."; } }

        #endregion
    }
}
