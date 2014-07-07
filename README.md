Nancy.RedisSessionProvider
==========================

Add the ability to use Redis as your session data store in NancyFX, allowing you to store more data consistently between requests than the standard cookie based session provider.

Usage is exactly the same as cookie based sessions:

    protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);

            RedisBasedSessions.Enable(pipelines);
        }
        
        
This will connect to a default Redis server on localhost with no password.

You can optionally pass in an additional parameter, RedisBasedSessionsConfiguration, where you can set the Redis connection string, the session expiration, session cookie name and other information.

License

As most of the code is adapted from the Nancy cookie session provider, this therefor retains NancyFX's licence and so is licensed under MIT.
