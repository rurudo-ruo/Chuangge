using System;
using System.Globalization;
using System.Resources;

namespace 窗格
{
    internal static class UiText
    {
        private static readonly ResourceManager Resources =
            new ResourceManager("窗格.UiText", typeof(UiText).Assembly);

        public static string Get(string key)
        {
            var value = Resources.GetString(key, CultureInfo.CurrentUICulture);
            if (value == null)
                throw new InvalidOperationException("Missing UI text resource: " + key);

            return value;
        }
    }
}
