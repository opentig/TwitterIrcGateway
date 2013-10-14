using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.Authentication
{
    public interface IAuthentication
    {
        AuthenticateResult Authenticate(Server server, Connection connection, UserInfo userInfo);
    }
}
