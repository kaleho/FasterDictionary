using FASTER.core;
using FasterDictionary.Tests.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FasterDictionary.Tests
{
    [CollectionDefinition("Non-Parallel Collection", DisableParallelization = true)]
    public class RecoverTests : IDisposable
    {


        static string DataDirectoryPath;
        static RecoverTests()
        {
            DataDirectoryPath = Path.Combine(Path.GetTempPath(), "FasterDictionary.Tests", "RecoverTests");
        }

        public RecoverTests()
        {
            ExcludeFiles();
        }

        public void Dispose()
        {
            ExcludeFiles();
        }

        private void ExcludeFiles()
        {
            if (Directory.Exists(DataDirectoryPath))
                Directory.Delete(DataDirectoryPath, true);
        }

        [Theory]

        [InlineData(100, CheckpointType.Snapshot)]
        [InlineData(100_000, CheckpointType.Snapshot)]
        [InlineData(1_000_000, CheckpointType.Snapshot)]

        [InlineData(100, CheckpointType.FoldOver)]
        [InlineData(100_000, CheckpointType.FoldOver)]
        [InlineData(1_000_000, CheckpointType.FoldOver)]


        //[InlineData(5_000_000)]
        public async Task AddRestartIterateOnce(int loops, CheckpointType checkpointType)
        {
            var options = GetOptions($"{nameof(AddRestartIterateOnce)}-{loops}");

            options.CheckPointType = checkpointType;
            options.DeleteOnClose = false;

            FasterDictionary<int, string>.ReadResult result;
            using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
            {
                for (var i = 0; i < loops; i++)
                    dictionary.Upsert(i, (i + 1).ToString()).Dismiss();

                await dictionary.Ping();

                await dictionary.Save();
            }

            options.DeleteOnClose = true;

            using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
            {
                for (var i = 0; i < 100; i++)
                {
                    var guid = GetGuid(i);
                    result = await dictionary.TryGet(i);
                    Assert.True(result.Found);
                    Assert.Equal((result.Key + 1).ToString(), result.Value);
                }

                var count = 0;
                await foreach (var entry in dictionary)
                {
                    count++;
                    Assert.Equal((entry.Key + 1).ToString(), entry.Value);
                }

                result = await dictionary.TryGet(loops);
                Assert.False(result.Found);

                Assert.Equal(loops, count);
            }
        }

        [Theory]
        [InlineData(226, 1, CheckpointType.FoldOver)]  //OK
        [InlineData(227, 1, CheckpointType.FoldOver)]  //OK
        [InlineData(200_000, 1, CheckpointType.FoldOver)]  //OK

        [InlineData(2832, 1, CheckpointType.Snapshot)] //OK
        [InlineData(2833, 1, CheckpointType.Snapshot)] //OK
        [InlineData(200_000, 1, CheckpointType.Snapshot)] //OK
        public async Task AddRestartGetValues(int loops, int step, CheckpointType checkpointType)
        {
            var options = GetOptions($"{nameof(AddRestartGetValues)}-{loops}");

            options.CheckPointType = checkpointType;
            options.DeleteOnClose = false;

            FasterDictionary<int, string>.ReadResult result;
            using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
            {
                for (var i = 0; i < loops; i++)
                {
                    var guid = GetGuid(i);
                    dictionary.Upsert(i, guid).Dismiss();
                }

                await dictionary.Ping();

                for (var i = 0; i < loops; i += step)
                {
                    var guid = GetGuid(i);
                    result = await dictionary.TryGet(i);
                    Assert.True(result.Found);
                    Assert.Equal(guid.ToString(), result.Value);
                }

                await dictionary.Save();
            }

            for (var k = 0; k < 3; k++)
                using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
                {
                    await dictionary.Ping();

                    for (var i = 0; i < loops; i++)
                    {
                        var guid = GetGuid(i);
                        result = await dictionary.TryGet(i);
                        Assert.True(result.Found);
                        Assert.Equal(guid, result.Value);
                    }
                }

        }



        [Theory]
        [InlineData(50_000, 1, CheckpointType.FoldOver)] //OK
        [InlineData(50_000, 1, CheckpointType.Snapshot)] //OK
        public async Task AddRestartColdRead(int loops, int step, CheckpointType checkpointType)
        {
            var options = GetOptions($"{nameof(AddRestartGetValues)}-{loops}");

            options.CheckPointType = checkpointType;
            options.DeleteOnClose = false;

            Stopwatch watch = new Stopwatch();
            watch.Reset();

            FasterDictionary<int, string>.ReadResult result;
            using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
            {
                for (var i = 0; i < loops; i++)
                {
                    var guid = GetGuid(i);
                    dictionary.Upsert(i, guid).Dismiss();
                }

                await dictionary.Ping();

                for (var i = 0; i < loops; i += step)
                {
                    var guid = GetGuid(loops - 1);
                    result = await dictionary.TryGet(loops - 1);
                    Assert.True(result.Found);
                    Assert.Equal(guid.ToString(), result.Value);
                }

                await dictionary.Save();
            }

            for (var k = 0; k < 3; k++)
                using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
                {
                    await dictionary.Ping();

                    for (var i = 0; i < loops; i += step)
                    {
                        var guid = GetGuid(loops - 1);
                        result = await dictionary.TryGet(loops - 1);
                        Assert.True(result.Found);
                        Assert.Equal(guid.ToString(), result.Value);
                    }
                }

        }


        [Theory]


        [InlineData(10_000, 10, 1, CheckpointType.Snapshot)]
        [InlineData(100_000, 1, 100, CheckpointType.Snapshot)]

        [InlineData(1_000_000, 10, 1, CheckpointType.Snapshot)]
        [InlineData(1_000_000, 1, 25, CheckpointType.Snapshot)]


        [InlineData(100_000, 10, 1, CheckpointType.FoldOver)]
        [InlineData(100_000, 1, 100, CheckpointType.FoldOver)]

        [InlineData(1_000_000, 10, 1, CheckpointType.FoldOver)]
        [InlineData(1_000_000, 1, 25, CheckpointType.FoldOver)]


        //[InlineData(5_000_000)]
        public async Task AddRestartIterateMany(int loops, int rcovrCount, int iteratCnt, CheckpointType checkType)
        {
            var options = GetOptions($"{nameof(AddRestartIterateOnce)}-{loops}");

            options.CheckPointType = checkType;
            options.DeleteOnClose = false;

            FasterDictionary<int, string>.ReadResult result;
            using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
            {
                for (var i = 0; i < loops; i++)
                    dictionary.Upsert(i, (i + 1).ToString()).Dismiss();

                await dictionary.Ping();

                await dictionary.Save();
            }

            for (var j = 0; j < rcovrCount; j++)
            {
                if (j == rcovrCount - 1)
                    options.DeleteOnClose = true;

                using (var dictionary = new FasterDictionary<int, string>(TestHelper.GetKeyComparer<int>(), options))
                {
                    for (var k = 0; k < iteratCnt; k++)
                    {

                        var count = 0;
                        await foreach (var entry in dictionary)
                        {
                            count++;
                            Assert.Equal((entry.Key + 1).ToString(), entry.Value);
                        }

                        result = await dictionary.TryGet(loops);
                        Assert.False(result.Found);

                        Assert.Equal(loops, count);
                    }
                }
            }
        }

        private static string GetGuid(int i, int count = 100)
        {
            return string.Join('-', Enumerable.Repeat(new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0).ToString(), count).ToArray());
        }

        private static FasterDictionary<int, string>.Options GetOptions(string directoryName, bool deleteOnClose = true)
        {
            return new FasterDictionary<int, string>.Options()
            {
                DictionaryName = directoryName,
                PersistDirectoryPath = DataDirectoryPath,
                DeleteOnClose = deleteOnClose,
                CheckPointType = FASTER.core.CheckpointType.Snapshot,
                Logger = new FasterLogger()
            };
        }

    }
}
