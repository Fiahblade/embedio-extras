﻿using Microsoft.IdentityModel.Tokens;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using Unosquare.Swan;

namespace Unosquare.Labs.EmbedIO.BearerToken
{
    /// <summary>
    /// EmbedIO module to allow authorizations with Bearer Tokens
    /// </summary>
    public class BearerTokenModule : WebModuleBase
    {
        private const string AuthorizationHeader = "Authorization";

        /// <summary>
        /// Module's Constructor
        /// </summary>
        /// <param name="authorizationServerProvider">The AuthorizationServerProvider to use</param>
        /// <param name="routes">The routes to authorization</param>
        /// <param name="secretKey">The secret key to encrypt tokens</param>
        /// <param name="endpoint">The url endpoint to get tokens</param>
        public BearerTokenModule(IAuthorizationServerProvider authorizationServerProvider,
            IEnumerable<string> routes = null, SymmetricSecurityKey secretKey = null, string endpoint = "/token")
        {
            if (secretKey == null)
            {
                // TODO: Make secretKey parameter mandatory and andd an overload that takes in a string for a secretKey
                secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9eyJjbGF"));
            }
            
            AddHandler(endpoint, HttpVerbs.Post, (context, ct) =>
            {
                var validationContext = context.GetValidationContext();
                authorizationServerProvider.ValidateClientAuthentication(validationContext);

                if (validationContext.IsValidated)
                {
                    context.JsonResponse(new BearerToken
                    {
                        Token = validationContext.GetToken(secretKey),
                        TokenType = "bearer",
                        ExpirationDate = authorizationServerProvider.GetExpirationDate(),
                        Username = validationContext.ClientId
                    });
                }
                else
                {
                    context.Rejected();
                }

                return Task.FromResult(true);
            });

            AddHandler(ModuleMap.AnyPath, HttpVerbs.Any, (context, ct) =>
            {
                if (routes != null && routes.Contains(context.RequestPath()) == false) return Task.FromResult(false);

                var authHeader = context.RequestHeader(AuthorizationHeader);

                if (string.IsNullOrWhiteSpace(authHeader) == false && authHeader.StartsWith("Bearer "))
                {
                    try
                    {
                        var token = authHeader.Replace("Bearer ", string.Empty);
                        var tokenHandler = new JwtSecurityTokenHandler();
                        SecurityToken validatedToken;
                        tokenHandler.ValidateToken(token, new TokenValidationParameters
                        {
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            IssuerSigningKey = secretKey
                        }, out validatedToken);

                        return Task.FromResult(false);
                    }
                    catch (Exception ex)
                    {
                        ex.Log(nameof(BearerTokenModule));
                    }
                }

                context.Rejected();

                return Task.FromResult(true);
            });
        }

        /// <summary>
        /// Returns Module Name
        /// </summary>
        public override string Name => nameof(BearerTokenModule).Humanize();
    }
}
