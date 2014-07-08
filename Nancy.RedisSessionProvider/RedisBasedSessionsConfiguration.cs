namespace Nancy.Session
{
    using Cryptography;

    /// <summary>
    /// Configuration options for redis based sessions
    /// </summary>
    public class RedisBasedSessionsConfiguration
    {
        internal const string DefaultCookieName = "_ncr";
        internal const string DefaultConnectionString = "localhost:6379";
        internal const int DefaultSessionDuration = 2592000; // 30 days
        internal const bool DefaultEnableSlidingSessions = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisBasedSessionsConfiguration"/> class.
        /// </summary>
        public RedisBasedSessionsConfiguration()
            : this(CryptographyConfiguration.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisBasedSessionsConfiguration"/> class.
        /// </summary>
        public RedisBasedSessionsConfiguration(CryptographyConfiguration cryptographyConfiguration)
        {
            CryptographyConfiguration = cryptographyConfiguration;
            CookieName = DefaultCookieName;
            ConnectionString = DefaultConnectionString;
            SessionDuration = DefaultSessionDuration;
            EnableSlidingSessions = DefaultEnableSlidingSessions;
        }

        /// <summary>
        /// Gets or sets the cryptography configuration
        /// </summary>
        public CryptographyConfiguration CryptographyConfiguration { get; set; }

        /// <summary>
        /// Formatter for de/serializing the session objects
        /// </summary>
        public IObjectSerializer Serializer { get; set; }

        /// <summary>
        /// Cookie name for storing session id
        /// </summary>
        public string CookieName { get; set; }

        /// <summary>
        /// Gets or sets the domain of the session cookie
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Gets or sets the path of the session cookie
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the Redis connection string
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the duration that the session is valid for from the current action
        /// </summary>
        public int SessionDuration { get; set; }

        /// <summary>
        /// Enables extending the session duration per access
        /// </summary>
        public bool EnableSlidingSessions { get; set; }

        /// <summary>
        /// Gets a value indicating whether the configuration is valid or not.
        /// </summary>
        public virtual bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(this.CookieName))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(this.ConnectionString))
                {
                    return false;
                }

                if (this.Serializer == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration.EncryptionProvider == null)
                {
                    return false;
                }

                if (this.CryptographyConfiguration.HmacProvider == null)
                {
                    return false;
                }

                return true;
            }
        }
    }
}