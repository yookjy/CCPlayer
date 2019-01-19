using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CCPlayer.UWP.Common.Helper
{
    public sealed class ReflectionHelper
    {
        [Windows.Foundation.Metadata.DefaultOverloadAttribute]
        public static object GetRuntimeProperty(string assemblyQualifiedName, string propertyName)
        {
            Type type = Type.GetType(assemblyQualifiedName, false);
            if (type != null)
            {
                return GetRuntimeProperty(type, propertyName);
            }
            return null;
        }

        public static object GetRuntimeProperty(Type type, string propertyName)
        {
            object result = null;
            try
            {
                var propInfo = type.GetRuntimeProperty(propertyName);
                if (propInfo == null && type.GetTypeInfo().BaseType != null)
                {
                    result = GetRuntimeProperty(type.GetTypeInfo().BaseType, propertyName);
                }
                else
                {
                    result = propInfo?.GetValue(type);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return result;
        }
    }
}
