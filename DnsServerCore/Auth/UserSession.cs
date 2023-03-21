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
using System.IO;
using System.Net;
using System.Security.Cryptography;
using TechnitiumLibrary.IO;
using TechnitiumLibrary.Net;

namespace DnsServerCore.Auth
{
    enum UserSessionType : byte
    {
        Unknown = 0,
        Standard = 1,
        ApiToken = 2
    }

    class UserSession : IComparable<UserSession>
    {
        #region variables

        static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        readonly string _token;
        readonly UserSessionType _type;
        readonly string _tokenName;
        readonly User _user;
        DateTime _lastSeen;
        IPAddress _lastSeenRemoteAddress;
        string _lastSeenUserAgent;

        #endregion

        #region constructor

        public UserSession(UserSessionType type, string tokenName, User user, IPAddress remoteAddress, string lastSeenUserAgent)
        {
            if ((tokenName is not null) && (tokenName.Length > 255))
                throw new ArgumentOutOfRangeException(nameof(tokenName), "Token name length cannot exceed 255 characters.");

            if (remoteAddress.IsIPv4MappedToIPv6)
                remoteAddress = remoteAddress.MapToIPv4();

            byte[] tokenBytes = new byte[32];
            _rng.GetBytes(tokenBytes);
            _token = Convert.ToHexString(tokenBytes).ToLower();

            _type = type;
            _tokenName = tokenName;
            _user = user;
            _lastSeen = DateTime.UtcNow;
            _lastSeenRemoteAddress = remoteAddress;
            _lastSeenUserAgent = lastSeenUserAgent;

            if ((_lastSeenUserAgent is not null) && (_lastSeenUserAgent.Length > 255))
                _lastSeenUserAgent = _lastSeenUserAgent.Substring(0, 255);
        }

        public UserSession(BinaryReader bR, AuthManager authManager)
        {
            switch (bR.ReadByte())
            {
                case 1:
                    _token = bR.ReadShortString();
                    _type = (UserSessionType)bR.ReadByte();

                    _tokenName = bR.ReadShortString();
                    if (_tokenName.Length == 0)
                        _tokenName = null;

                    _user = authManager.GetUser(bR.ReadShortString());
                    _lastSeen = bR.ReadDateTime();
                    _lastSeenRemoteAddress = IPAddressExtensions.ReadFrom(bR);

                    _lastSeenUserAgent = bR.ReadShortString();
                    if (_lastSeenUserAgent.Length == 0)
                        _lastSeenUserAgent = null;

                    break;

                default:
                    throw new InvalidDataException("Invalid data or version not supported.");
            }
        }

        #endregion

        #region public

        public void UpdateLastSeen(IPAddress remoteAddress, string lastSeenUserAgent)
        {
            if (remoteAddress.IsIPv4MappedToIPv6)
                remoteAddress = remoteAddress.MapToIPv4();

            _lastSeen = DateTime.UtcNow;
            _lastSeenRemoteAddress = remoteAddress;
            _lastSeenUserAgent = lastSeenUserAgent;

            if ((_lastSeenUserAgent is not null) && (_lastSeenUserAgent.Length > 255))
                _lastSeenUserAgent = _lastSeenUserAgent.Substring(0, 255);
        }

        public bool HasExpired()
        {
            if (_type == UserSessionType.ApiToken)
                return false;

            if (_user.SessionTimeoutSeconds == 0)
                return false;

            return _lastSeen.AddSeconds(_user.SessionTimeoutSeconds) < DateTime.UtcNow;
        }

        public void WriteTo(BinaryWriter bW)
        {
            bW.Write((byte)1);
            bW.WriteShortString(_token);
            bW.Write((byte)_type);

            if (_tokenName is null)
                bW.Write((byte)0);
            else
                bW.WriteShortString(_tokenName);

            bW.WriteShortString(_user.Username);
            bW.Write(_lastSeen);
            _lastSeenRemoteAddress.WriteTo(bW);

            if (_lastSeenUserAgent is null)
                bW.Write((byte)0);
            else
                bW.WriteShortString(_lastSeenUserAgent);
        }

        public int CompareTo(UserSession other)
        {
            return other._lastSeen.CompareTo(_lastSeen);
        }

        #endregion

        #region properties

        public string Token
        { get { return _token; } }

        public UserSessionType Type
        { get { return _type; } }

        public string TokenName
        { get { return _tokenName; } }

        public User User
        { get { return _user; } }

        public DateTime LastSeen
        { get { return _lastSeen; } }

        public IPAddress LastSeenRemoteAddress
        { get { return _lastSeenRemoteAddress; } }

        public string LastSeenUserAgent
        { get { return _lastSeenUserAgent; } }

        #endregion
    }
}
