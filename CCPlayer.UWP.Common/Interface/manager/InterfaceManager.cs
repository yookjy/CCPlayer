using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

namespace CCPlayer.UWP.Common.Interface.manager
{
    public sealed class InterfaceManager
    {
        private static PropertySet propertySet = new PropertySet(); 

        public static void RegisterModule(string key, string value)
        {
            propertySet[key] = value;
        }

        public static string GetRegiseredValue(string key)
        {
            return propertySet[key] as string;
        }
    }
}
