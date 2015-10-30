using System.Globalization;
using StackExchange.Redis;

namespace Nancy.Session
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Bootstrapper;
    using Cryptography;
    using Cookies;
    using Helpers;

    /// <summary>
    /// Redis based session storage
    /// </summary>
    public class RedisBasedSessions : IObjectSerializerSelector
    {
        private readonly RedisBasedSessionsConfiguration _currentConfiguration;
        private static ConnectionMultiplexer _redis;
        private readonly IDatabase _db;

        /// <summary>
        /// Gets the redis name that the session is stored in
        /// </summary>
        /// <value>Redis name</value>
        public string CookieName
        {
            get
            {
                return _currentConfiguration.CookieName;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisBasedSessions"/> class.
        /// </summary>
        /// <param name="encryptionProvider">The encryption provider.</param>
        /// <param name="hmacProvider">The hmac provider</param>
        /// <param name="objectSerializer">Session object serializer to use</param>
        public RedisBasedSessions(IEncryptionProvider encryptionProvider, IHmacProvider hmacProvider, IObjectSerializer objectSerializer)
        {
            _currentConfiguration = new RedisBasedSessionsConfiguration
            {
                Serializer = objectSerializer,
                CryptographyConfiguration = new CryptographyConfiguration(encryptionProvider, hmacProvider)
            };

            if (_redis == null)
                _redis = ConnectionMultiplexer.Connect(_currentConfiguration.ConnectionString);

            _db = _redis.GetDatabase();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RedisBasedSessions"/> class.
        /// </summary>
        /// <param name="configuration">Redis based sessions configuration.</param>
        public RedisBasedSessions(RedisBasedSessionsConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }

            if (!configuration.IsValid)
            {
                throw new ArgumentException("Configuration is invalid", "configuration");
            }
            _currentConfiguration = configuration;

            if (_redis == null)
                _redis = ConnectionMultiplexer.Connect(_currentConfiguration.ConnectionString);

            _db = _redis.GetDatabase();
        }



        /// <summary>
        /// Initialise and add redis based session hooks to the application pipeine
        /// </summary>
        /// <param name="pipelines">Application pipelines</param>
        /// <param name="configuration">Redis based sessions configuration.</param>
        /// <returns>Formatter selector for choosing a non-default serializer</returns>
        public static IObjectSerializerSelector Enable(IPipelines pipelines, RedisBasedSessionsConfiguration configuration)
        {
            if (pipelines == null)
            {
                throw new ArgumentNullException("pipelines");
            }

            var sessionStore = new RedisBasedSessions(configuration);

            pipelines.BeforeRequest.AddItemToStartOfPipeline(ctx => LoadSession(ctx, sessionStore));
            pipelines.AfterRequest.AddItemToEndOfPipeline(ctx => SaveSession(ctx, sessionStore));

            return sessionStore;
        }

        /// <summary>
        /// Initialise and add redis based session hooks to the application pipeine
        /// </summary>
        /// <param name="pipelines">Application pipelines</param>
        /// <param name="cryptographyConfiguration">Cryptography configuration</param>
        /// <returns>Formatter selector for choosing a non-default serializer</returns>
        public static IObjectSerializerSelector Enable(IPipelines pipelines, CryptographyConfiguration cryptographyConfiguration)
        {
            var redisBasedSessionsConfiguration = new RedisBasedSessionsConfiguration(cryptographyConfiguration)
            {
                Serializer = new DefaultObjectSerializer()
            };
            return Enable(pipelines, redisBasedSessionsConfiguration);
        }

        /// <summary>
        /// Initialise and add redis based session hooks to the application pipeine with the default encryption provider.
        /// </summary>
        /// <param name="pipelines">Application pipelines</param>
        /// <returns>Formatter selector for choosing a non-default serializer</returns>
        public static IObjectSerializerSelector Enable(IPipelines pipelines)
        {
            return Enable(pipelines, new RedisBasedSessionsConfiguration
            {
                Serializer = new DefaultObjectSerializer()
            });
        }

        /// <summary>
        /// Using the specified serializer
        /// </summary>
        /// <param name="newSerializer">Formatter to use</param>
        public void WithSerializer(IObjectSerializer newSerializer)
        {
            _currentConfiguration.Serializer = newSerializer;
        }

        /// <summary>
        /// Save the session into the response
        /// </summary>
        /// <param name="session">Session to save</param>
        /// <param name="response">Response to save into</param>
        public void Save(ISession session, Response response)
        {
            Guid sessionId;

            if (session == null || !session.HasChanged)
            {
                return;
            }

            if (session["__SessionId"] is Guid)
            {
                sessionId = (Guid)session["__SessionId"];
            }
            else
            {
                sessionId = Guid.NewGuid();
                session["__SessionId"] = sessionId;
                if (_currentConfiguration.SessionDuration != 0)
                    session["__SessionExpiration"] = DateTime.Now.AddSeconds(_currentConfiguration.SessionDuration);
            }

            var sb = new StringBuilder();
            foreach (var kvp in session)
            {
                sb.Append(HttpUtility.UrlEncode(kvp.Key));
                sb.Append("=");

                var objectString = _currentConfiguration.Serializer.Serialize(kvp.Value);

                sb.Append(HttpUtility.UrlEncode(objectString));
                sb.Append(";");
            }

            var cryptographyConfiguration = _currentConfiguration.CryptographyConfiguration;

            var redisData = cryptographyConfiguration.EncryptionProvider.Encrypt(sb.ToString());

            // Store the value in Redis
            _db.StringSet(_currentConfiguration.Prefix + sessionId.ToString(), redisData);

            if (_currentConfiguration.SessionDuration != 0)
                _db.KeyExpire(_currentConfiguration.Prefix + sessionId.ToString(), TimeSpan.FromSeconds(_currentConfiguration.SessionDuration));

            var encryptedSessionId = cryptographyConfiguration.EncryptionProvider.Encrypt(sessionId.ToString());
            var hmacBytes = cryptographyConfiguration.HmacProvider.GenerateHmac(sessionId.ToString());
            var cookieData = String.Format("{0}{1}", Convert.ToBase64String(hmacBytes), encryptedSessionId);

            var cookie = new NancyCookie(_currentConfiguration.CookieName, cookieData, true)
            {
                Domain = _currentConfiguration.Domain,
                Path = _currentConfiguration.Path
            };

            if (_currentConfiguration.EnableSlidingSessions)
                session["__SessionExpiration"] = DateTime.Now.AddSeconds(_currentConfiguration.SessionDuration);

            if (session["__SessionExpiration"] is DateTime)
                cookie.Expires = (DateTime)session["__SessionExpiration"];

            response.WithCookie(cookie);
        }

        /// <summary>
        /// Loads the session from the request
        /// </summary>
        /// <param name="request">Request to load from</param>
        /// <returns>ISession containing the load session values</returns>
        public ISession Load(Request request)
        {
            var dictionary = new Dictionary<string, object>();

            var cookieName = _currentConfiguration.CookieName;
            var hmacProvider = _currentConfiguration.CryptographyConfiguration.HmacProvider;
            var encryptionProvider = _currentConfiguration.CryptographyConfiguration.EncryptionProvider;

            if (request.Cookies.ContainsKey(cookieName))
            {
                var cookieData = request.Cookies[cookieName];
                var hmacLength = Base64Helpers.GetBase64Length(hmacProvider.HmacLength);
                var hmacString = cookieData.Substring(0, hmacLength);
                var encryptedSessionId = cookieData.Substring(hmacLength);

                var sessionId = encryptionProvider.Decrypt(encryptedSessionId);

                var hmacBytes = Convert.FromBase64String(hmacString);
                var newHmac = hmacProvider.GenerateHmac(sessionId);
                var hmacValid = HmacComparer.Compare(newHmac, hmacBytes, hmacProvider.HmacLength);

                // Get the value from Redis
                string encryptedData = _db.StringGet(_currentConfiguration.Prefix + sessionId.ToString(CultureInfo.InvariantCulture));

                if (encryptedData != null)
                {
                    var data = encryptionProvider.Decrypt(encryptedData);

                    var parts = data.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var part in parts.Select(part => part.Split('=')))
                    {
                        var valueObject = _currentConfiguration.Serializer.Deserialize(HttpUtility.UrlDecode(part[1]));

                        dictionary[HttpUtility.UrlDecode(part[0])] = valueObject;
                    }

                    if (!hmacValid)
                    {
                        dictionary.Clear();
                    }
                }
            }

            return new Session(dictionary);
        }

        /// <summary>
        /// Saves the request session into the response
        /// </summary>
        /// <param name="context">Nancy context</param>
        /// <param name="sessionStore">Session store</param>
        private static void SaveSession(NancyContext context, RedisBasedSessions sessionStore)
        {
            sessionStore.Save(context.Request.Session, context.Response);
        }

        /// <summary>
        /// Loads the request session
        /// </summary>
        /// <param name="context">Nancy context</param>
        /// <param name="sessionStore">Session store</param>
        /// <returns>Always returns null</returns>
        private static Response LoadSession(NancyContext context, RedisBasedSessions sessionStore)
        {
            if (context.Request == null)
            {
                return null;
            }

            context.Request.Session = sessionStore.Load(context.Request);

            return null;
        }
    }
}
