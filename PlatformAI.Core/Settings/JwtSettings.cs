using System;

namespace PlatformAI.Core.Settings;

public class JwtSettings
{

        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string SecretKey { get; set; }
        public int ExpirationMinutes { get; set; }
}
