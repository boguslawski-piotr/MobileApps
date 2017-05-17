using System;
using System.Diagnostics;

#if __UNIFIED__

using LocalAuthentication;
using Foundation;

#endif

namespace pbXSecurity
{
#if __UNIFIED__

    public partial class SecretsManager : ISecretsManager
    {
		public bool DeviceOwnerAuthenticationAvailable
		{
			get {
				var context = new LAContext();
                return context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthentication, out NSError error);
            }
		}

		public bool DeviceOwnerAuthenticationWithBiometricsAvailable
		{
			get {
				var context = new LAContext();
                return context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out NSError error);
            }
		}

		bool _AuthenticateDeviceOwner(LAContext context, LAPolicy policy, string Msg, Action Success, Action<string> Error)
        {
            //byte[] cc = new byte[] { (byte)'a', (byte)'l', (byte)'a' };
            //NSData c = new NSData(Convert.ToBase64String(cc), NSDataBase64DecodingOptions.None);
            //context.SetCredentialType(c, LACredentialType.ApplicationPassword);

            NSError error;
			if (context.CanEvaluatePolicy(policy, out error))
			{
				Debug.WriteLine($"iOS/macOS: _AuthenticateDeviceOwner: policy: {policy}");

                var replyHandler = new LAContextReplyHandler((bool success, NSError _error) =>
				{
					context.BeginInvokeOnMainThread(() =>
					{
						if (success)
						{
                            Debug.WriteLine($"iOS/macOS: _AuthenticateDeviceOwner: success");
							Success();
						}
						else
						{
							Debug.WriteLine($"iOS/macOS: _AuthenticateDeviceOwner: error: {_error}");
                            if (_error.Code == Convert.ToInt32(LAStatus.UserFallback) 
                                && policy == LAPolicy.DeviceOwnerAuthenticationWithBiometrics)
							{
                                _AuthenticateDeviceOwner(context, LAPolicy.DeviceOwnerAuthentication, Msg, Success, Error);
							}
							else
								Error(_error.ToString());
						}
                    });

				});

				context.EvaluatePolicy(policy, new NSString(Msg), replyHandler);

				return true;
			}
            else
			{
				Debug.WriteLine($"iOS/macOS: _AuthenticateDeviceOwner: (policy: {policy}), error: {error}");
			}

			return false;            
        }

        public bool AuthenticateDeviceOwner(string Msg, Action Success, Action<string> Error)
		{
			// It seems that the call with parameter LAPolicy.DeviceOwnerAuthentication automatically uses biometrics authentication when it is set in the system settings.
            // TODO: check it on a real device(s)

			var context = new LAContext();
            //if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out NSError error))
				return _AuthenticateDeviceOwner(context, LAPolicy.DeviceOwnerAuthentication, Msg, Success, Error);
            //else
			//    return _AuthenticateDeviceOwner(context, LAPolicy.DeviceOwnerAuthenticationWithBiometrics, Msg, Success, Error);
		}
	}

#endif
}