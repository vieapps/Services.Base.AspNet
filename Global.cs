﻿#region Related components
using System;
using System.Net;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Dynamic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Reactive.Subjects;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.DependencyInjection;

using WampSharp.V2.Realm;
using WampSharp.V2.Core.Contracts;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using net.vieapps.Components.Utility;
using net.vieapps.Components.Security;
#endregion

namespace net.vieapps.Services
{
	public static partial class Global
	{

		#region Environment
		/// <summary>
		/// Gets or sets name of the working service
		/// </summary>
		public static string ServiceName { get; set; }

		/// <summary>
		/// Gets the cancellation token source (global scope)
		/// </summary>
		public static CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

		/// <summary>
		/// Gets or sets the service provider
		/// </summary>
		public static IServiceProvider ServiceProvider { get; set; }

		/// <summary>
		/// Gets or sets the root path of the app
		/// </summary>
		public static string RootPath { get; set; }

		/// <summary>
		/// Adds the accessor of HttpContext into collection of services
		/// </summary>
		/// <param name="services"></param>
		public static void AddHttpContextAccessor(this IServiceCollection services) => services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

		/// <summary>
		/// Gets the current HttpContext object
		/// </summary>
		public static HttpContext CurrentHttpContext => Global.ServiceProvider.GetService<IHttpContextAccessor>().HttpContext;

		/// <summary>
		/// Gets the correlation identity
		/// </summary>
		/// <param name="items"></param>
		/// <returns></returns>
		internal static string GetCorrelationID(IDictionary<object, object> items)
		{
			return items != null
				? !items.ContainsKey("Correlation-ID")
					? (items["Correlation-ID"] = UtilityService.NewUUID) as string
					: items["Correlation-ID"] as string
				: UtilityService.NewUUID;
		}

		/// <summary>
		/// Gets the correlation identity of this context
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static string GetCorrelationID(this HttpContext context) => Global.GetCorrelationID(context?.Items);

		/// <summary>
		/// Gets the correlation identity of the current context
		/// </summary>
		/// <returns></returns>
		public static string GetCorrelationID() => Global.GetCorrelationID(Global.CurrentHttpContext?.Items);

		/// <summary>
		/// Gets the execution times of current HTTP pipeline context
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static string GetExecutionTimes(this HttpContext context)
		{
			if (context.Items.ContainsKey("PipelineStopwatch") && context.Items["PipelineStopwatch"] is Stopwatch stopwatch)
			{
				stopwatch.Stop();
				return stopwatch.GetElapsedTimes();
			}
			return "";
		}

		/// <summary>
		/// Gets the execution times of current HTTP pipeline context
		/// </summary>
		/// <returns></returns>
		public static string GetExecutionTimes() => Global.GetExecutionTimes(Global.CurrentHttpContext);

		/// <summary>
		/// Gets related information of this request
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static Tuple<NameValueCollection, NameValueCollection, string, string, Uri> GetRequestInfo(this HttpContext context)
		{
			var header = context.Request.Headers.ToNameValueCollection();
			var queryString = context.Request.QueryString.ToNameValueCollection();
			var userAgent = context.Request.Headers["User-Agent"].First();
			var ipAddress = $"{context.Connection.RemoteIpAddress}";
			var urlReferer = !string.IsNullOrWhiteSpace(context.Request.Headers["Referer"].First())
				? new Uri(context.Request.Headers["Referer"].First())
				: null;
			return new Tuple<NameValueCollection, NameValueCollection, string, string, Uri>(header, queryString, userAgent, ipAddress, urlReferer);
		}

		/// <summary>
		/// Gets related information of this request
		/// </summary>
		/// <returns></returns>
		public static Tuple<NameValueCollection, NameValueCollection, string, string, Uri> GetRequestInfo() => Global.GetRequestInfo(Global.CurrentHttpContext);

		/// <summary>
		/// Gets the information of the requested app
		/// </summary>
		/// <param name="header"></param>
		/// <param name="query"></param>
		/// <param name="agentString"></param>
		/// <param name="ipAddress"></param>
		/// <param name="urlReferrer"></param>
		/// <returns></returns>
		public static Tuple<string, string, string> GetAppInfo(NameValueCollection header, NameValueCollection query, string agentString, string ipAddress, Uri urlReferrer)
		{
			var name = UtilityService.GetAppParameter("x-app-name", header, query, "Generic App");

			var platform = UtilityService.GetAppParameter("x-app-platform", header, query);
			if (string.IsNullOrWhiteSpace(platform))
				platform = string.IsNullOrWhiteSpace(agentString)
					? "N/A"
					: agentString.IsContains("iPhone") || agentString.IsContains("iPad") || agentString.IsContains("iPod")
						? "iOS PWA"
						: agentString.IsContains("Android")
							? "Android PWA"
							: agentString.IsContains("Windows Phone")
								? "Windows Phone PWA"
								: agentString.IsContains("BlackBerry") || agentString.IsContains("BB10")
									? "BlackBerry PWA"
									: agentString.IsContains("IEMobile") || agentString.IsContains("Opera Mini") || agentString.IsContains("MDP/")
										? "Mobile PWA"
										: "Desktop PWA";

			var origin = header?["origin"];
			if (string.IsNullOrWhiteSpace(origin))
				origin = urlReferrer?.AbsoluteUri;
			if (string.IsNullOrWhiteSpace(origin) || origin.IsStartsWith("file://") || origin.IsStartsWith("http://localhost"))
				origin = ipAddress;

			return new Tuple<string, string, string>(name, platform, origin);
		}

		/// <summary>
		/// Gets the information of the requested app
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static Tuple<string, string, string> GetAppInfo(this HttpContext context)
		{
			var info = context.GetRequestInfo();
			return Global.GetAppInfo(info.Item1, info.Item2, info.Item3, info.Item4, info.Item5);
		}

		/// <summary>
		/// Gets the information of the requested app
		/// </summary>
		/// <returns></returns>
		public static Tuple<string, string, string> GetAppInfo() => Global.GetAppInfo(Global.CurrentHttpContext);

		/// <summary>
		/// Gets the information of the app's OS
		/// </summary>
		/// <param name="agentString"></param>
		/// <returns></returns>
		public static string GetOSInfo(this string agentString)
		{
			return agentString.IsContains("iPhone") || agentString.IsContains("iPad") || agentString.IsContains("iPod")
				? "iOS"
				: agentString.IsContains("Android")
					? "Android"
					: agentString.IsContains("Windows Phone")
						? "Windows Phone"
						: agentString.IsContains("BlackBerry") || agentString.IsContains("BB10")
							? "BlackBerry" + (agentString.IsContains("BB10") ? "10" : "OS")
							: agentString.IsContains("IEMobile") || agentString.IsContains("Opera Mini") || agentString.IsContains("MDP/")
								? "Mobile OS"
								: agentString.IsContains("Windows")
									? "Windows"
									: agentString.IsContains("Mac OS")
										? "macOS"
										: agentString.IsContains("Linux")
											? "Linux"
											: "Generic OS";
		}

		/// <summary>
		/// Gets the information of the app's OS
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static string GetOSInfo(this HttpContext context) => context.Request.Headers["User-Agent"].First().GetOSInfo();

		/// <summary>
		/// Gets the information of the app's OS
		/// </summary>
		/// <returns></returns>
		public static string GetOSInfo() => Global.GetOSInfo(Global.CurrentHttpContext);

		static HashSet<string> _StaticSegments = null;

		/// <summary>
		/// Gets the segments of static files
		/// </summary>
		public static HashSet<string> StaticSegments => Global._StaticSegments ?? (Global._StaticSegments = (UtilityService.GetAppSetting("Segments:Static", "").Trim().ToLower() + "|statics").ToHashSet('|', true));
		#endregion

		#region Encryption keys
		static string _EncryptionKey = null, _ValidationKey = null, _JWTKey = null;
		static byte[] _ECCKey = null;
		static string _RSAKey = null, _RSAExponent = null, _RSAModulus = null;
		static RSA _RSA = null;

		/// <summary>
		/// Geths the key for encrypting/decrypting data with AES
		/// </summary>
		public static string EncryptionKey => Global._EncryptionKey ?? (Global._EncryptionKey = UtilityService.GetAppSetting("Keys:Encryption", "VIEApps-c98c6942-Default-0ad9-AES-40ed-Encryption-9e53-Key-65c501fcf7b3"));

		/// <summary>
		/// Gets the key for validating
		/// </summary>
		public static string ValidationKey => Global._ValidationKey ?? (Global._ValidationKey = UtilityService.GetAppSetting("Keys:Validation", "VIEApps-49d8bd8c-Default-babc-Data-43f4-Validation-bc30-Key-355b0891dc0f"));

		/// <summary>
		/// Gets the key for validating/signing a JSON Web Token
		/// </summary>
		/// <returns></returns>
		public static string JWTKey => Global._JWTKey ?? (Global._JWTKey = Global.ValidationKey.GetHMACHash(Global.EncryptionKey, "BLAKE256").ToBase64Url());

		/// <summary>
		/// Gets the key for encrypting/decrypting data with ECCsecp256k1
		/// </summary>
		public static BigInteger ECCKey => ECCsecp256k1.GetPrivateKey(Global._ECCKey ?? (Global._ECCKey = UtilityService.GetAppSetting("Keys:ECC", "MD9g3THNC0Z1Ulk+5eGpijotaR5gtv/mzMzfMa5Oio3gOCCSbpCZe5SBIsvdzyof3rFVFgBxOXBM0QgyhBgaCSVkUGaLko5YAmX8qJ6ThORAwrOJNGqNx08y3l0b+A3jkWdvqVVnu6oS7QfnAPaOp4QjMC0Uxpl/2E3QpsI+vNZ9HkWx4mTJeW1AegNmmvov+KhzgWXt8HuT6Vys/MWGxoWPq+ooDGPAfmeVZiY+8GyY4zgMisdqUObEejaAj+gQd+nnnpI8YOFimjir8fp5eP/rT1t6urYcHNUGjsHvPZUAC7uczE3M3ZIhPXz4iT5MDBtonUGsTnrKZKh/NGGvaC/DAhptFIsnjOlLbAyiXmY=").Base64ToBytes().Decrypt()));

		/// <summary>
		/// Gets the key for encrypting/decrypting data with ECCsecp256k1
		/// </summary>
		public static ECCsecp256k1.Point ECCPublicKey => ECCsecp256k1.GeneratePublicKey(Global.ECCKey);

		/// <summary>
		/// Gets the key for encrypting/decrypting data with RSA
		/// </summary>
		public static string RSAKey => Global._RSAKey ?? (Global._RSAKey = UtilityService.GetAppSetting("Keys:RSA", "DA90WJt+jHmBfNlAS31qY3OS+3iUfwN7Gg+bKUm5RxqV13y7eh4daubWAHqtbrPS/Qw5F3d3D26yEo5FZroGvhyFGpfqJqeoz9EhsByn8hZZwns09qtITU6Wbqi74mQe9/h7Xp/57sJUDKssiTFKZYC+OS9RFytJDFXZF8zVoMDQmdG8f7lD6t16bIk27+KwX3OzdSoPOtNalSAwWxZVKchL23NXbHR6EAhnqouLWGHXTOBLIuOnJdqFE8IzgwuffFJ53iq47K7ILC2mAm3DEyv+j24VBYE/EcB8GBLGVlo4uv3tNaDIw9isTlxyETtZwR+NbV7JXOl3j/wKjCL2U/nsfPzQhAMC58+0oKeda2fCV4cXtg/EyrQSpjn56S04BybThgJjoYF1Vf1FqmaNLB9GaV73PLQKUPLY3qFws7k6og5A08eNsgUVfcZqO1iqVUJDbJHCuPgygnRMSsamGS8oWBtSb/rDto+jdpx2oC/KhNA2zMkhYiIO7DtK7sdwo0XeDjid7aipP+bsIuAGmRmt1RgklF65DGcvbglEPSziopUH2hfvbKhtxD+9gp4RrO7KZPrcFKaP8YOKAh05bAvNKwH6Bou3TKPXSjxzalAJqdHzjZNOLmNsfgS2+Y0J9BJhrGMTZtKqjtkbM2qYLkD8DONGdmUmud0TYjBLQVwesScjXxZsYyyohnU+vzqVD6AOxkc9FcU2RMEnSrCu7HAKTTo930v3p4S1iQrKDXn0zrIvDuX5m0LzeUJcV1WJUsu+n6lQCwDKWYZkNpGnJfodl2TtCjt82etcZMyU13Tpoo1M7oyFqlKjcUmy3hzmqfTqbG2AM348VTg9O3jgJxe9kBu5/Gf5tJXvNKaG3sXIh5Ym8pJ08tpE2DS3v3hlPCOD8YsqouW4FzBMmBgNykY5XjtgYZgDHPxCSlIQSuu19Iv6fXk5lDWjJ1Lx3RqRiXbRk7Xj6wlwu/WlomRRzwyO9fL5W89Gj1BaeYVGK+tBnGs9DFVBIIqlrpDyMOVRhkFayZ5J96r+guuZqmHiq+e4JYIC7aYHMT78n8F8DbWbV7hcnyLTe+e5zFQ4WmuBcPlP3ne4YT+Rs/G2NWvdHKmMDOj91CfyuCCgIFSA2/N8gmElrwt3t2yofkhC2tbJEwLCbErupxC/ttjQkjnqEy84me1mR3rkjRNrhbWer3OLAFNwaVMpX6XkcDuGn7evG9Km73Sv8f7y3G2jH9pj5D67T6iLywiyL0s/4Hs+m+VdRRDagWc9P/I+D9ub9tdD8zYTe89UVHzBGpAA3rA7xlowSZNpN2RQC/j0x2J32uy7sSBOh4U8OcJaAJCZjGZjobrhOr6jQJgNpzs8Zx9L/zTGHRDHb0DI6WOAG++KYkcNYqPS1/aewNE8wSMMaZVRkV4Lp7zx4jj3G6+hj80ZOtpRVto7sVoTH34wbzhz0M+NpunGN/ozvmumGeHqZVSQCwnOSnZjiDg+NJU24nmAwv0m0Bc2fY57M50M14gdfBa0ezuCyElMdySr6Kt1ftFtR5NHl/jHjzD+PPq5Bgzgu8uK06iJtRwOvG4K5RrVcIpoj1absbc+Lh22Ri887iLTxZf7uQyau13FXUbpk2eAwKy1oi5RVYT8MTiijSFhct8xCFj359WYSWq5On7onMn39cWPFEFOKxw48aWu/pyLFjRdZgFxlNvEUgBIie/kI+bj3vlBAaTD+3MWFnCrkLcd1flp4nuyQj0iL2xX8pE49FlSNhkkcF2eHF48JaHrNbpnoFLlUKPg98225M0LR2Qxz/rz9uH7P+YEkrQgcO1fYnRbuFx2o5BJ5PdB45B9GmmpdIZJlP2gagxiWqDdotASjD3pfr17S8jL02bko9oBpmf1Eh5lQYyjYDnNjHmYv3nLRcCd8BKxyksAfqv8lOhpvLsKnwHhFVG2yefKOdmC/M3SGwxDabUI7Xv0kA8+COvGq6AC+sLXHydfPN901UjcvRJwNk85yTJO94zwLUUFgVFQNJtEVbarpPsDGYcAeuyF+ccN74HlVvdi8h9WyT1en39hWO8elhTrEZTDB/1ZNfi9Q6iTJYHrLCqw8vaABdBpN4bEm/XEV2gQE923YuItiPAznDCEl0En5VzYQSOT+mENq6XZTVdu1peSFvmexDoNwreK0waGtCYgmbxMnhXq").Decrypt());

		/// <summary>
		/// Gest the instance of RSA
		/// </summary>
		public static RSA RSA => Global._RSA ?? (Global._RSA = Global.CreateRSA());

		/// <summary>
		/// Creates the instance of RSA
		/// </summary>
		/// <returns></returns>
		public static RSA CreateRSA()
		{
			Global._RSA = string.IsNullOrWhiteSpace(Global.RSAKey)
				? RSA.Create()
				: CryptoService.CreateRSA(Global.RSAKey);
			if (Global._RSA.KeySize != 2048)
			{
				Global._RSA = RSA.Create();
				Global._RSA.KeySize = 2048;
			}
			Global.Logger.LogInformation($"RSA is initialized [{Global._RSA.GetType()}] - Key size: {Global._RSA.KeySize} bits");
			return Global._RSA;
		}

		/// <summary>
		/// Gets the exponent of RSA
		/// </summary>
		public static string RSAExponent => Global._RSAExponent ?? (Global._RSAExponent = Global.RSA.ExportParameters(false).Exponent.ToHex());

		/// <summary>
		/// Gets the modulus of the RSA
		/// </summary>
		public static string RSAModulus => Global._RSAModulus ?? (Global._RSAModulus = Global.RSA.ExportParameters(false).Modulus.ToHex());
		#endregion

		#region Working with session & authenticate ticket
		/// <summary>
		/// Gets the session information
		/// </summary>
		/// <param name="header"></param>
		/// <param name="query"></param>
		/// <param name="agentString"></param>
		/// <param name="ipAddress"></param>
		/// <param name="urlReferrer"></param>
		/// <param name="sessionID"></param>
		/// <param name="user"></param>
		/// <returns></returns>
		public static Session GetSession(NameValueCollection header, NameValueCollection query, string agentString, string ipAddress, Uri urlReferrer, string sessionID = null, IUser user = null)
		{
			var appInfo = Global.GetAppInfo(header, query, agentString, ipAddress, urlReferrer);
			return new Session
			{
				SessionID = sessionID ?? "",
				IP = ipAddress,
				AppAgent = agentString,
				DeviceID = UtilityService.GetAppParameter("x-device-id", header, query, ""),
				AppName = appInfo.Item1,
				AppPlatform = appInfo.Item2,
				AppOrigin = appInfo.Item3,
				User = user != null ? new User(user) : new User("", sessionID ?? "", new List<string> { SystemRole.All.ToString() }, new List<Privilege>())
			};
		}

		/// <summary>
		/// Gets the session information
		/// </summary>
		/// <param name="query"></param>
		/// <param name="agentString"></param>
		/// <param name="ipAddress"></param>
		/// <param name="urlReferrer"></param>
		/// <param name="sessionID"></param>
		/// <param name="user"></param>
		/// <returns></returns>
		public static Session GetSession(NameValueCollection query, string agentString, string ipAddress, Uri urlReferrer, string sessionID = null, IUser user = null)
			=> Global.GetSession(null, query, agentString, ipAddress, urlReferrer, sessionID, user);

		/// <summary>
		/// Gets the session information
		/// </summary>
		/// <param name="context"></param>
		/// <param name="sessionID"></param>
		/// <param name="user"></param>
		/// <returns></returns>
		public static Session GetSession(this HttpContext context, string sessionID = null, IUser user = null)
		{
			var session = context.GetItem<Session>("Session");
			if (session == null)
			{
				var info = context.GetRequestInfo();
				session = Global.GetSession(info.Item1, info.Item2, info.Item3, info.Item4, info.Item5, sessionID, user);
			}
			return session;
		}

		/// <summary>
		/// Gets the session information
		/// </summary>
		/// <param name="sessionID"></param>
		/// <param name="user"></param>
		/// <returns></returns>
		public static Session GetSession(string sessionID = null, IUser user = null) => Global.CurrentHttpContext.GetSession(sessionID, user);

		/// <summary>
		/// Checks to see the session is existed or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="session"></param>
		/// <returns></returns>
		public static async Task<bool> IsSessionExistAsync(this HttpContext context, Session session)
		{
			if (!string.IsNullOrWhiteSpace(session?.SessionID))
			{
				var result = await context.CallServiceAsync(new RequestInfo(session, "Users", "Session", "EXIST")).ConfigureAwait(false);
				return result?["Existed"] is JValue isExisted && isExisted.Value != null && isExisted.Value.CastAs<bool>() == true;
			}
			return false;
		}

		/// <summary>
		/// Checks to see the session is existed or not
		/// </summary>
		/// <param name="session"></param>
		/// <returns></returns>
		public static Task<bool> IsSessionExistAsync(Session session)
			=> Global.CurrentHttpContext.IsSessionExistAsync(session);

		/// <summary>
		/// Gets the authenticate ticket of this session
		/// </summary>
		/// <param name="session"></param>
		/// <param name="onPreCompleted"></param>
		/// <returns></returns>
		public static string GetAuthenticateToken(this Session session, Action<JObject> onPreCompleted = null)
			=> session.User.GetAuthenticateToken(Global.EncryptionKey, Global.JWTKey, payload =>
			{
				payload["2fa"] = $"{session.Verification}|{UtilityService.NewUUID}".Encrypt(Global.EncryptionKey, true);
				onPreCompleted?.Invoke(payload);
			});

		/// <summary>
		/// Updates this session with information of authenticate token
		/// </summary>
		/// <param name="context"></param>
		/// <param name="session"></param>
		/// <param name="authenticateToken"></param>
		/// <param name="onAuthenticateTokenParsed"></param>
		/// <param name="updateWithAccessTokenAsync"></param>
		/// <param name="onAccessTokenParsed"></param>
		public static async Task UpdateWithAuthenticateTokenAsync(this HttpContext context, Session session, string authenticateToken, Action<JObject, User> onAuthenticateTokenParsed = null, Func<HttpContext, Session, string, Action<JObject, User>, Task> updateWithAccessTokenAsync = null, Action<JObject, User> onAccessTokenParsed = null)
		{
			// get user from authenticate token
			session.User = authenticateToken.ParseAuthenticateToken(Global.EncryptionKey, Global.JWTKey, (payload, user) =>
			{
				if (!user.ID.Equals(""))
					try
					{
						session.Verification = "true".IsEquals(payload.Get<string>("2fa")?.Decrypt(Global.EncryptionKey, true).ToArray("|").First());
					}
					catch { }
				onAuthenticateTokenParsed?.Invoke(payload, user);
			});

			// update session identity
			session.SessionID = session.User.SessionID;

			// get session of authenticated user and verify with access token
			if (!session.User.ID.Equals(""))
			{
				if (updateWithAccessTokenAsync != null)
					await updateWithAccessTokenAsync(context, session, authenticateToken, onAccessTokenParsed).ConfigureAwait(false);
				else
					await context.UpdateWithAccessTokenAsync(session, authenticateToken, onAccessTokenParsed).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Updates this session with information of authenticate token
		/// </summary>
		/// <param name="session"></param>
		/// <param name="authenticateToken"></param>
		/// <param name="onAuthenticateTokenParsed"></param>
		/// <param name="updateWithAccessTokenAsync"></param>
		/// <param name="onAccessTokenParsed"></param>
		public static Task UpdateWithAuthenticateTokenAsync(Session session, string authenticateToken, Action<JObject, User> onAuthenticateTokenParsed = null, Func<HttpContext, Session, string, Action<JObject, User>, Task> updateWithAccessTokenAsync = null, Action<JObject, User> onAccessTokenParsed = null)
			=> Global.CurrentHttpContext.UpdateWithAuthenticateTokenAsync(session, authenticateToken, onAuthenticateTokenParsed, updateWithAccessTokenAsync, onAccessTokenParsed);

		/// <summary>
		/// Updates this session with information of access token
		/// </summary>
		/// <param name="context"></param>
		/// <param name="session"></param>
		/// <param name="authenticateToken"></param>
		/// <param name="onAccessTokenParsed"></param>
		public static async Task UpdateWithAccessTokenAsync(this HttpContext context, Session session, string authenticateToken, Action<JObject, User> onAccessTokenParsed = null)
		{
			// get session of authenticated user and verify with access token
			var sessionInfo = await context.CallServiceAsync(new RequestInfo(session, "Users", "Session", "GET")
			{
				Header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "x-app-token", authenticateToken }
				},
				Extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{
					{ "Signature", authenticateToken.GetHMACSHA256(Global.ValidationKey) }
				}
			}, Global.CancellationTokenSource.Token).ConfigureAwait(false);

			// check existing
			if (sessionInfo == null)
				throw new SessionNotFoundException();

			// check expiration
			if (DateTime.Parse(sessionInfo.Get<string>("ExpiredAt")) < DateTime.Now)
				throw new SessionExpiredException();

			// get user with privileges
			var user = sessionInfo.Get<string>("AccessToken").ParseAccessToken(Global.ECCKey, onAccessTokenParsed);

			// check identity
			if (!session.User.ID.Equals(user.ID) || !session.User.SessionID.Equals(user.SessionID))
				throw new InvalidSessionException();

			// update user
			session.User = user;
		}

		/// <summary>
		/// Updates this session with information of access token
		/// </summary>
		/// <param name="session"></param>
		/// <param name="authenticateToken"></param>
		/// <param name="onAccessTokenParsed"></param>
		public static Task UpdateWithAccessTokenAsync(Session session, string authenticateToken, Action<JObject, User> onAccessTokenParsed = null)
			=> Global.CurrentHttpContext.UpdateWithAccessTokenAsync(session, authenticateToken, onAccessTokenParsed);
		#endregion

		#region Authentication & Authorization
		/// <summary>
		/// Gets the state that determines the user is authenticated or not
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static bool IsAuthenticated(this HttpContext context) => context != null && context.User.Identity.IsAuthenticated;

		/// <summary>
		/// Gets the state that determines the user is authenticated or not
		/// </summary>
		/// <returns></returns>
		public static bool IsAuthenticated() => Global.IsAuthenticated(Global.CurrentHttpContext);

		/// <summary>
		/// Gets the state that determines the user is system administrator or not
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		public static Task<bool> IsSystemAdministratorAsync(this HttpContext context)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).IsSystemAdministratorAsync(context.GetCorrelationID())
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is system administrator or not
		/// </summary>
		/// <returns></returns>
		public static Task<bool> IsSystemAdministratorAsync() => Global.IsSystemAdministratorAsync(Global.CurrentHttpContext);

		/// <summary>
		/// Gets the state that determines the user is service administrator or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of service</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> IsServiceAdministratorAsync(this HttpContext context, string serviceName = null, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).IsServiceAdministratorAsync(serviceName, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is service administrator or not
		/// </summary>
		/// <param name="serviceName">The name of service</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> IsServiceAdministratorAsync(string serviceName = null, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.IsServiceAdministratorAsync(Global.CurrentHttpContext, serviceName, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is service administrator or not
		/// </summary>
		/// <param name="context"></param>
		/// /// <param name="serviceName">The name of service</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> IsServiceModeratorAsync(this HttpContext context, string serviceName = null, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).IsServiceModeratorAsync(serviceName, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is service administrator or not
		/// </summary>
		/// /// <param name="serviceName">The name of service</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> IsServiceModeratorAsync(string serviceName = null, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.IsServiceModeratorAsync(Global.CurrentHttpContext, serviceName, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to manage or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanManageAsync(this HttpContext context, string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanManageAsync(serviceName, objectName, objectIdentity, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to manage or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanManageAsync(string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanManageAsync(Global.CurrentHttpContext, serviceName, objectName, objectIdentity, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to manage or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanManageAsync(this HttpContext context, string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanManageAsync(serviceName, systemID, definitionID, objectID, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to manage or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanManageAsync(string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanManageAsync(Global.CurrentHttpContext, serviceName, systemID, definitionID, objectID, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to moderate or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanModerateAsync(this HttpContext context, string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanModerateAsync(serviceName, objectName, objectIdentity, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to moderate or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanModerateAsync(string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanModerateAsync(Global.CurrentHttpContext, serviceName, objectName, objectIdentity, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to moderate or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanModerateAsync(this HttpContext context, string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanModerateAsync(serviceName, systemID, definitionID, objectID, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to moderate or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanModerateAsync(string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanModerateAsync(Global.CurrentHttpContext, serviceName, systemID, definitionID, objectID, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to edit or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanEditAsync(this HttpContext context, string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanEditAsync(serviceName, objectName, objectIdentity, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to edit or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanEditAsync(string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanEditAsync(Global.CurrentHttpContext, serviceName, objectName, objectIdentity, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to edit or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanEditAsync(this HttpContext context, string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanEditAsync(serviceName, systemID, definitionID, objectID, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to edit or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanEditAsync(string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanEditAsync(Global.CurrentHttpContext, serviceName, systemID, definitionID, objectID, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to contribute or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanContributeAsync(this HttpContext context, string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanContributeAsync(serviceName, objectName, objectIdentity, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to contribute or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanContributeAsync(string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanContributeAsync(Global.CurrentHttpContext, serviceName, objectName, objectIdentity, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to contribute or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanContributeAsync(this HttpContext context, string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanContributeAsync(serviceName, systemID, definitionID, objectID, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to contribute or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanContributeAsync(string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanContributeAsync(Global.CurrentHttpContext, serviceName, systemID, definitionID, objectID, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to view or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanViewAsync(this HttpContext context, string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanViewAsync(serviceName, objectName, objectIdentity, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to view or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanViewAsync(string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanViewAsync(Global.CurrentHttpContext, serviceName, objectName, objectIdentity, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to view or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanViewAsync(this HttpContext context, string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanViewAsync(serviceName, systemID, definitionID, objectID, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to view or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanViewAsync(string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanViewAsync(Global.CurrentHttpContext, serviceName, systemID, definitionID, objectID, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to download or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanDownloadAsync(this HttpContext context, string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanDownloadAsync(serviceName, objectName, objectIdentity, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to download or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="objectName">The name of the service's object</param>
		/// <param name="objectIdentity">The identity of the service's object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanDownloadAsync(string serviceName, string objectName, string objectIdentity, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanDownloadAsync(Global.CurrentHttpContext, serviceName, objectName, objectIdentity, getPrivileges, getActions);

		/// <summary>
		/// Gets the state that determines the user is able to download or not
		/// </summary>
		/// <param name="context"></param>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanDownloadAsync(this HttpContext context, string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> context != null && context.User.Identity != null && context.User.Identity is UserIdentity
				? (context.User.Identity as IUser).CanDownloadAsync(serviceName, systemID, definitionID, objectID, getPrivileges, getActions, context.GetCorrelationID(), Global.CancellationTokenSource.Token)
				: Task.FromResult(false);

		/// <summary>
		/// Gets the state that determines the user is able to download or not
		/// </summary>
		/// <param name="serviceName">The name of the service</param>
		/// <param name="systemID">The identity of the business system</param>
		/// <param name="definitionID">The identity of the entity definition</param>
		/// <param name="objectID">The identity of the business object</param>
		/// <param name="getPrivileges">The function to prepare the collection of privileges</param>
		/// <param name="getActions">The function to prepare the actions of each privilege</param>
		/// <returns></returns>
		public static Task<bool> CanDownloadAsync(string serviceName, string systemID, string definitionID, string objectID, Func<IUser, Privileges, List<Privilege>> getPrivileges = null, Func<PrivilegeRole, List<string>> getActions = null)
			=> Global.CanDownloadAsync(Global.CurrentHttpContext, serviceName, systemID, definitionID, objectID, getPrivileges, getActions);
		#endregion

		#region Error handling
		/// <summary>
		/// Writes an error exception as JSON to output with status code
		/// </summary>
		/// <param name="context"></param>
		/// <param name="logger"></param>
		/// <param name="exception"></param>
		/// <param name="requestInfo"></param>
		/// <param name="writeLogs"></param>
		public static void WriteError(this HttpContext context, ILogger logger, WampException exception, RequestInfo requestInfo = null, bool writeLogs = true)
		{
			// prepare
			var details = exception.GetDetails(requestInfo);
			var code = details.Item1;
			var message = details.Item2;
			var type = details.Item3;
			var stack = details.Item4;
			var inner = details.Item5;
			var jsonException = details.Item6;

			JArray jsonStack = null;
			if (Global.IsDebugStacksEnabled & !string.IsNullOrWhiteSpace(stack))
			{
				jsonStack = new JArray
				{
					new JObject
					{
						{ "Message", exception.Message },
						{ "Type", exception.GetType().ToString() },
						{ "Stack", exception.StackTrace }
					}
				};
				while (inner != null)
				{
					jsonStack.Add(new JObject
					{
						{ "Message", inner.Message },
						{ "Type", inner.GetType().ToString() },
						{ "Stack", inner.StackTrace }
					});
					inner = inner.InnerException;
				}
			}

			// write logs
			if (writeLogs)
			{
				var logs = new List<string> { "[" + type + "]: " + message };

				stack = "";
				if (requestInfo != null)
					stack += "\r\n" + "==> Request:\r\n" + requestInfo.ToJson().ToString(Global.IsDebugStacksEnabled ? Formatting.Indented : Formatting.None);

				if (jsonException != null)
					stack += "\r\n" + "==> Response:\r\n" + jsonException.ToString(Global.IsDebugStacksEnabled ? Formatting.Indented : Formatting.None);

				if (exception != null)
				{
					stack += "\r\n" + "==> Stack:\r\n" + exception.StackTrace;
					var counter = 0;
					var innerException = exception.InnerException;
					while (innerException != null)
					{
						counter++;
						stack += "\r\n" + $"-------- Inner ({counter}) ----------------------------------"
							+ "> Message: " + innerException.Message + "\r\n"
							+ "> Type: " + innerException.GetType().ToString() + "\r\n"
							+ innerException.StackTrace;
						innerException = innerException.InnerException;
					}
				}

				context.WriteLogs(logger, requestInfo?.ObjectName ?? "unknown", logs, exception, requestInfo?.ServiceName ?? Global.ServiceName);
			}

			// show error
			context.WriteHttpError(code, message, type, requestInfo?.CorrelationID ?? context.GetCorrelationID(), jsonStack);
		}

		/// <summary>
		/// Writes an error exception as JSON to output with status code
		/// </summary>
		/// <param name="context"></param>
		/// <param name="exception"></param>
		/// <param name="requestInfo"></param>
		/// <param name="writeLogs"></param>
		public static void WriteError(this HttpContext context, WampException exception, RequestInfo requestInfo = null, bool writeLogs = true)
			=> context.WriteError(Global.Logger, exception, requestInfo, writeLogs);

		/// <summary>
		/// Writes an error exception as JSON to output with status code
		/// </summary>
		/// <param name="context"></param>
		/// <param name="logger"></param>
		/// <param name="exception"></param>
		/// <param name="requestInfo"></param>
		/// <param name="message"></param>
		/// <param name="writeLogs"></param>
		public static void WriteError(this HttpContext context, ILogger logger, Exception exception, RequestInfo requestInfo = null, string message = null, bool writeLogs = true)
		{
			if (exception is WampException)
				context.WriteError(logger, exception as WampException, requestInfo, writeLogs);

			else
			{
				message = message ?? (exception != null ? exception.Message : "Unknown error");
				if (writeLogs && exception != null)
					context.WriteLogs(logger, requestInfo?.ObjectName ?? "Unknown", new List<string>
					{
						message,
						$"Request:\r\n{requestInfo?.ToJson().ToString(Global.IsDebugStacksEnabled ? Formatting.Indented : Formatting.None) ?? "None"}"
					}, exception, requestInfo?.ServiceName ?? Global.ServiceName);

				var type = exception != null ? exception.GetType().ToString().ToArray('.').Last() : "Unknown";
				var statusCode = exception != null ? exception.GetHttpStatusCode() : 500;
				var correlationID = requestInfo?.CorrelationID ?? context.GetCorrelationID();
				context.WriteHttpError(statusCode, message, type, correlationID, exception, Global.IsDebugStacksEnabled);
			}
		}

		/// <summary>
		/// Writes an error exception as JSON to output with status code
		/// </summary>
		/// <param name="context"></param>
		/// <param name="exception"></param>
		/// <param name="requestInfo"></param>
		/// <param name="message"></param>
		/// <param name="writeLogs"></param>
		public static void WriteError(this HttpContext context, Exception exception, RequestInfo requestInfo = null, string message = null, bool writeLogs = true)
			=> context.WriteError(Global.Logger, exception, requestInfo, message, writeLogs);
		#endregion

		#region WAMP connections & updaters
		/// <summary>
		/// Opens the WAMP channels with default settings
		/// </summary>
		/// <param name="onIncommingConnectionEstablished"></param>
		/// <param name="onOutgoingConnectionEstablished"></param>
		/// <param name="watingTimes"></param>
		/// <returns></returns>
		public static void OpenWAMPChannels(Action<object, WampSessionCreatedEventArgs> onIncommingConnectionEstablished = null, Action<object, WampSessionCreatedEventArgs> onOutgoingConnectionEstablished = null, int watingTimes = 6789)
		{
			Task.WaitAll(new[]
			{
				WAMPConnections.OpenIncomingChannelAsync(
					onIncommingConnectionEstablished,
					(sender, arguments) =>
					{
						if (WAMPConnections.ChannelsAreClosedBySystem || arguments.CloseType.Equals(SessionCloseType.Goodbye))
							Global.Logger.LogInformation($"The incoming channel to WAMP router is closed - {arguments.CloseType} ({(string.IsNullOrWhiteSpace(arguments.Reason) ? "Unknown" : arguments.Reason)})");

						else if (WAMPConnections.IncommingChannel != null)
						{
							Global.Logger.LogInformation($"The incoming channel to WAMP router is broken - {arguments.CloseType} ({(string.IsNullOrWhiteSpace(arguments.Reason) ? "Unknown" : arguments.Reason)})");
							WAMPConnections.IncommingChannel.ReOpen(Global.CancellationTokenSource.Token, (msg, ex) => Global.Logger.LogDebug(msg, ex), "Incoming");
						}
					},
					(sender, arguments) => Global.Logger.LogDebug($"The incoming channel to WAMP router got an error: {arguments.Exception.Message}", arguments.Exception)
				),
				WAMPConnections.OpenOutgoingChannelAsync(
					onOutgoingConnectionEstablished,
					(sender, arguments) =>
					{
						if (WAMPConnections.ChannelsAreClosedBySystem || arguments.CloseType.Equals(SessionCloseType.Goodbye))
							Global.Logger.LogInformation($"The outgoging channel to WAMP router is closed - {arguments.CloseType} ({(string.IsNullOrWhiteSpace(arguments.Reason) ? "Unknown" : arguments.Reason)})");

						else if (WAMPConnections.OutgoingChannel != null)
						{
							Global.Logger.LogInformation($"The outgoging channel to WAMP router is broken - {arguments.CloseType} ({(string.IsNullOrWhiteSpace(arguments.Reason) ? "Unknown" : arguments.Reason)})");
							WAMPConnections.OutgoingChannel.ReOpen(Global.CancellationTokenSource.Token, (msg, ex) => Global.Logger.LogDebug(msg, ex), "Outgoging");
						}
					},
					(sender, arguments) => Global.Logger.LogDebug($"The outgoging channel to WAMP router got an error: {arguments.Exception.Message}", arguments.Exception)
				)
			}, watingTimes > 0 ? watingTimes : 6789, Global.CancellationTokenSource.Token);
		}

		/// <summary>
		/// Gets or sets publisher (for publishing update messages)
		/// </summary>
		public static ISubject<UpdateMessage> UpdateMessagePublisher { get; set; }

		/// <summary>
		/// Publishs an update message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="logger"></param>
		public static void PublishUpdateMessage(this UpdateMessage message, ILogger logger = null)
		{
			if (Global.UpdateMessagePublisher == null)
				try
				{
					Global.UpdateMessagePublisher = WAMPConnections.OutgoingChannel.RealmProxy.Services.GetSubject<UpdateMessage>("net.vieapps.rtu.update.messages");
					Global.UpdateMessagePublisher.OnNext(message);
					if (Global.IsDebugResultsEnabled)
						Global.WriteLogs(logger ?? Global.Logger, "RTU", $"Successfully send an update message: {message.ToJson().ToString(Global.IsDebugLogEnabled ? Formatting.Indented : Formatting.None)}");
				}
				catch (Exception ex)
				{
					Global.WriteLogs(logger ?? Global.Logger, "RTU", $"Failure send an update message: {ex.Message} => {message.ToJson().ToString(Formatting.Indented)}", ex);
				}

			else
				try
				{
					Global.UpdateMessagePublisher.OnNext(message);
					if (Global.IsDebugResultsEnabled)
						Global.WriteLogs(logger ?? Global.Logger, "RTU", $"Successfully send an update message: {message.ToJson().ToString(Global.IsDebugLogEnabled ? Formatting.Indented : Formatting.None)}");
				}
				catch (Exception ex)
				{
					Global.WriteLogs(logger ?? Global.Logger, "RTU", $"Failure send an update message: {ex.Message} => {message.ToJson().ToString(Formatting.Indented)}", ex);
				}
		}

		/// <summary>
		/// Publishs an update message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="logger"></param>
		public static void Publish(this UpdateMessage message, ILogger logger = null) => message.PublishUpdateMessage(logger);

		/// <summary>
		/// Publishs an update message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="logger"></param>
		/// <returns></returns>
		public static async Task PublishUpdateMessageAsync(this UpdateMessage message, ILogger logger = null)
		{
			if (Global.UpdateMessagePublisher == null)
				try
				{
					await WAMPConnections.OpenOutgoingChannelAsync().ConfigureAwait(false);
					Global.UpdateMessagePublisher = WAMPConnections.OutgoingChannel.RealmProxy.Services.GetSubject<UpdateMessage>("net.vieapps.rtu.update.messages");
					Global.UpdateMessagePublisher.OnNext(message);
					if (Global.IsDebugResultsEnabled)
						await Global.WriteLogsAsync(logger ?? Global.Logger, "RTU", $"Successfully send an update message: {message.ToJson().ToString(Global.IsDebugLogEnabled ? Formatting.Indented : Formatting.None)}").ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					await Global.WriteLogsAsync(logger ?? Global.Logger, "RTU", $"Failure send an update message: {ex.Message} => {message.ToJson().ToString(Formatting.Indented)}", ex).ConfigureAwait(false);
				}

			else
				try
				{
					Global.UpdateMessagePublisher.OnNext(message);
					if (Global.IsDebugResultsEnabled)
						await Global.WriteLogsAsync(logger ?? Global.Logger, "RTU", $"Successfully send an update message: {message.ToJson().ToString(Global.IsDebugLogEnabled ? Formatting.Indented : Formatting.None)}").ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					await Global.WriteLogsAsync(logger ?? Global.Logger, "RTU", $"Failure send an update message: {ex.Message} => {message.ToJson().ToString(Formatting.Indented)}", ex).ConfigureAwait(false);
				}
		}

		/// <summary>
		/// Publishs an update message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="logger"></param>
		/// <returns></returns>
		public static Task PublishAsync(this UpdateMessage message, ILogger logger = null) => message.PublishUpdateMessageAsync(logger);

		/// <summary>
		/// Gets or sets updater (for updating inter-communicate messages)
		/// </summary>
		public static IDisposable InterCommunicateMessageUpdater { get; set; }

		/// <summary>
		/// Publishs an inter-communicate message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="logger"></param>
		/// <returns></returns>
		public static async Task PublishInterCommunicateMessageAsync(this CommunicateMessage message, ILogger logger = null)
		{
			try
			{
				await Global.RTUService.SendInterCommunicateMessageAsync(message, Global.CancellationTokenSource.Token).ConfigureAwait(false);
				if (Global.IsDebugResultsEnabled)
					await Global.WriteLogsAsync(logger ?? Global.Logger, "RTU", $"Successfully send an inter-communicate message: {message.ToJson().ToString(Global.IsDebugLogEnabled ? Formatting.Indented : Formatting.None)}").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Global.WriteLogsAsync(logger ?? Global.Logger, "RTU", $"Failure send an inter-communicate message: {ex.Message}", ex).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Publishs an inter-communicate message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="logger"></param>
		/// <returns></returns>
		public static Task PublishAsync(this CommunicateMessage message, ILogger logger = null) => message.PublishInterCommunicateMessageAsync(logger);
		#endregion

	}
}