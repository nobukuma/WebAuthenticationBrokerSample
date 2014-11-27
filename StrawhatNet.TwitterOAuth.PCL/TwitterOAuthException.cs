using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StrawhatNet.TwitterOAuth.PCL
{
    public class TwitterOAuthException : Exception
    {
        public TwitterOAuthException()
        {
        }

        public TwitterOAuthException(string message)
            : base(message)
        {

        }

        public TwitterOAuthException(string message, Exception innerException)
            : base(message, innerException)
        {

        }
    }
}
