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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TechnitiumLibrary;
using TechnitiumLibrary.Net.Dns.ResourceRecords;

namespace DnsServerCore.Dns.ResourceRecords
{
    static class DnsResourceRecordExtensions
    {
        public static void SetGlueRecords(this DnsResourceRecord record, string glueAddresses)
        {
            if (record.RDATA is not DnsNSRecordData nsRecord)
                throw new InvalidOperationException();

            string domain = nsRecord.NameServer;

            IReadOnlyList<IPAddress> glueAddressesList = glueAddresses.Split(IPAddress.Parse, ',');
            DnsResourceRecord[] glueRecords = new DnsResourceRecord[glueAddressesList.Count];

            for (int i = 0; i < glueRecords.Length; i++)
            {
                switch (glueAddressesList[i].AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        glueRecords[i] = new DnsResourceRecord(domain, DnsResourceRecordType.A, DnsClass.IN, record.TTL, new DnsARecordData(glueAddressesList[i]));
                        break;

                    case AddressFamily.InterNetworkV6:
                        glueRecords[i] = new DnsResourceRecord(domain, DnsResourceRecordType.AAAA, DnsClass.IN, record.TTL, new DnsAAAARecordData(glueAddressesList[i]));
                        break;
                }
            }

            record.GetAuthRecordInfo().GlueRecords = glueRecords;
        }

        public static void SyncGlueRecords(this DnsResourceRecord record, IReadOnlyList<DnsResourceRecord> allGlueRecords)
        {
            if (record.RDATA is not DnsNSRecordData nsRecord)
                throw new InvalidOperationException();

            string domain = nsRecord.NameServer;

            List<DnsResourceRecord> foundGlueRecords = new List<DnsResourceRecord>(2);

            foreach (DnsResourceRecord glueRecord in allGlueRecords)
            {
                switch (glueRecord.Type)
                {
                    case DnsResourceRecordType.A:
                    case DnsResourceRecordType.AAAA:
                        if (glueRecord.Name.Equals(domain, StringComparison.OrdinalIgnoreCase))
                            foundGlueRecords.Add(glueRecord);

                        break;
                }
            }

            record.GetAuthRecordInfo().GlueRecords = foundGlueRecords;
        }

        public static void SyncGlueRecords(this DnsResourceRecord record, IReadOnlyCollection<DnsResourceRecord> deletedGlueRecords, IReadOnlyCollection<DnsResourceRecord> addedGlueRecords)
        {
            if (record.RDATA is not DnsNSRecordData nsRecord)
                throw new InvalidOperationException();

            bool updated = false;

            List<DnsResourceRecord> updatedGlueRecords = new List<DnsResourceRecord>();
            IReadOnlyList<DnsResourceRecord> existingGlueRecords = record.GetAuthRecordInfo().GlueRecords;
            if (existingGlueRecords is not null)
            {
                foreach (DnsResourceRecord existingGlueRecord in existingGlueRecords)
                {
                    if (deletedGlueRecords.Contains(existingGlueRecord))
                        updated = true; //skipped to delete existing glue record
                    else
                        updatedGlueRecords.Add(existingGlueRecord);
                }
            }

            string domain = nsRecord.NameServer;

            foreach (DnsResourceRecord addedGlueRecord in addedGlueRecords)
            {
                switch (addedGlueRecord.Type)
                {
                    case DnsResourceRecordType.A:
                    case DnsResourceRecordType.AAAA:
                        if (addedGlueRecord.Name.Equals(domain, StringComparison.OrdinalIgnoreCase))
                        {
                            updatedGlueRecords.Add(addedGlueRecord);
                            updated = true;
                        }
                        break;
                }
            }

            if (updated)
                record.GetAuthRecordInfo().GlueRecords = updatedGlueRecords;
        }

        public static AuthRecordInfo GetAuthRecordInfo(this DnsResourceRecord record)
        {
            if (record.Tag is not AuthRecordInfo rrInfo)
            {
                rrInfo = new AuthRecordInfo();
                record.Tag = rrInfo;
            }

            return rrInfo;
        }

        public static CacheRecordInfo GetCacheRecordInfo(this DnsResourceRecord record)
        {
            if (record.Tag is not CacheRecordInfo rrInfo)
            {
                rrInfo = new CacheRecordInfo();
                record.Tag = rrInfo;
            }

            return rrInfo;
        }

        public static void CopyRecordInfoFrom(this DnsResourceRecord record, DnsResourceRecord otherRecord)
        {
            record.Tag = otherRecord.Tag;
        }
    }
}
