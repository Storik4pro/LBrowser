using System.Net.WebSockets;
using Windows.Networking.Sockets;
using System;
using Windows.Web.Http;
using Windows.Web;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LinesBrowser
{
    public enum NetworkErrorCode
    {
        INVALID_URI,
        HOST_NAME_NOT_RESOLVED,
        CANNOT_CONNECT,
        SERVER_UNREACHABLE,
        TIMEOUT,
        CONNECTION_ABORTED,
        CONNECTION_RESET,
        DISCONNECTED,
        OPERATION_CANCELED,
        ERROR_HTTP_INVALID_SERVER_RESPONSE,
        REDIRECT_FAILED,
        UNEXPECTED_STATUS_CODE,
        CERTIFICATE_COMMON_NAME_INCORRECT,
        CERTIFICATE_EXPIRED,
        CERTIFICATE_CONTAINS_ERRORS,
        CERTIFICATE_REVOKED,
        CERTIFICATE_INVALID,
        HTTP_TO_HTTPS_ON_REDIRECTION,
        HTTPS_TO_HTTP_ON_REDIRECTION,
        UNKNOWN,
        ACCESS_DENIED,
        ERROR_HTTP_HEADER_NOT_FOUND
    }
    public class ErrorHelper 
    {
        public static NetworkErrorCode? MapExceptionToCode(Exception ex, out uint? rawHResult)
        {
            rawHResult = null;
            for (Exception current = ex; current != null; current = current.InnerException)
            {
                if (current is WebSocketException wsEx)
                {
                    var status = Windows.Networking.Sockets.WebSocketError.GetStatus(wsEx.HResult);
                    switch (status)
                    {
                        case WebErrorStatus.HostNameNotResolved:
                            return NetworkErrorCode.HOST_NAME_NOT_RESOLVED;
                        case WebErrorStatus.CannotConnect:
                            return NetworkErrorCode.CANNOT_CONNECT;
                        case WebErrorStatus.ServerUnreachable:
                            return NetworkErrorCode.SERVER_UNREACHABLE;
                        case WebErrorStatus.Timeout:
                            return NetworkErrorCode.TIMEOUT;
                        case WebErrorStatus.ConnectionAborted:
                            return NetworkErrorCode.CONNECTION_ABORTED;
                        case WebErrorStatus.ConnectionReset:
                            return NetworkErrorCode.CONNECTION_RESET;
                        case WebErrorStatus.Disconnected:
                            return NetworkErrorCode.DISCONNECTED;
                        case WebErrorStatus.OperationCanceled:
                            return NetworkErrorCode.OPERATION_CANCELED;
                        case WebErrorStatus.ErrorHttpInvalidServerResponse:
                            return NetworkErrorCode.ERROR_HTTP_INVALID_SERVER_RESPONSE;
                        case WebErrorStatus.RedirectFailed:
                            return NetworkErrorCode.REDIRECT_FAILED;
                        case WebErrorStatus.UnexpectedStatusCode:
                            return NetworkErrorCode.UNEXPECTED_STATUS_CODE;
                        case WebErrorStatus.CertificateCommonNameIsIncorrect:
                            return NetworkErrorCode.CERTIFICATE_COMMON_NAME_INCORRECT;
                        case WebErrorStatus.CertificateExpired:
                            return NetworkErrorCode.CERTIFICATE_EXPIRED;
                        case WebErrorStatus.CertificateContainsErrors:
                            return NetworkErrorCode.CERTIFICATE_CONTAINS_ERRORS;
                        case WebErrorStatus.CertificateRevoked:
                            return NetworkErrorCode.CERTIFICATE_REVOKED;
                        case WebErrorStatus.CertificateIsInvalid:
                            return NetworkErrorCode.CERTIFICATE_INVALID;
                        case WebErrorStatus.HttpToHttpsOnRedirection:
                            return NetworkErrorCode.HTTP_TO_HTTPS_ON_REDIRECTION;
                        case WebErrorStatus.HttpsToHttpOnRedirection:
                            return NetworkErrorCode.HTTPS_TO_HTTP_ON_REDIRECTION;
                        default:
                            break;
                    }
                }
                Debug.WriteLine(unchecked((uint)current.HResult));
                if (current.HResult != 0)
                {
                    
                    uint hr = unchecked((uint)current.HResult);
                    rawHResult = hr;
                    switch (hr)
                    {
                        case 0x80072EE7u:
                            return NetworkErrorCode.HOST_NAME_NOT_RESOLVED;
                        case 0x80072EFDu:
                            return NetworkErrorCode.CANNOT_CONNECT;
                        case 0x80072EFEu:
                            return NetworkErrorCode.CONNECTION_ABORTED;
                        case 0x80072EE2u:
                            return NetworkErrorCode.TIMEOUT;
                        case 0x80072F76u:
                            return NetworkErrorCode.ERROR_HTTP_HEADER_NOT_FOUND;
                        case 0x80072F78u:
                            return NetworkErrorCode.ERROR_HTTP_INVALID_SERVER_RESPONSE;
                        case 0x80072F8Fu:
                            return NetworkErrorCode.CERTIFICATE_EXPIRED;
                        case 0x80072F8Eu:
                            return NetworkErrorCode.CERTIFICATE_COMMON_NAME_INCORRECT;
                        case 0x80004005u:
                            break;
                        case 0x80070005u:
                            return NetworkErrorCode.ACCESS_DENIED;
                        case 0x80004004u:
                            return NetworkErrorCode.OPERATION_CANCELED;
                        default:
                            break;
                    }
                }
            }
            if (ex is UriFormatException)
                return NetworkErrorCode.INVALID_URI;
            if (ex is TimeoutException)
                return NetworkErrorCode.TIMEOUT;
            return null;
        }
    }
}