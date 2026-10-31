using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using Xunit;

namespace NoFences.Tests
{
    public class UiTextTests
    {
        [Fact]
        public void Chinese_keys_all_exist_in_the_neutral_resources()
        {
            var neutral = LoadKeys(CultureInfo.InvariantCulture);
            var chinese = LoadKeys(new CultureInfo("zh-CN"));

            Assert.NotEmpty(neutral);
            Assert.NotEmpty(chinese);
            Assert.All(chinese, key => Assert.Contains(key, neutral));
        }

        [Fact]
        public void Diagnostic_keys_are_translated()
        {
            var chinese = LoadKeys(new CultureInfo("zh-CN"));

            Assert.Contains("窗格 encountered an error", chinese);
            Assert.Contains("Advanced settings", chinese);
        }

        private static HashSet<string> LoadKeys(CultureInfo culture)
        {
            var manager = new ResourceManager("窗格.UiText", typeof(UiText).Assembly);
            var resourceSet = manager.GetResourceSet(culture, true, false);
            Assert.NotNull(resourceSet);

            return new HashSet<string>(
                resourceSet.Cast<DictionaryEntry>().Select(entry => (string)entry.Key));
        }
    }
}
