using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Xunit;
using static Libplanet.Tests.TestUtils;

namespace Libplanet.Tests.Store
{
    public class TrieStateStoreTest
    {
        private readonly IKeyValueStore _stateKeyValueStore;

        public TrieStateStoreTest()
        {
            _stateKeyValueStore = new DefaultKeyValueStore(null);
        }

        public static KeyBytes KeyFoo => StateStoreExtensions.EncodeKey("foo");

        public static KeyBytes KeyBar => StateStoreExtensions.EncodeKey("bar");

        public static KeyBytes KeyBaz => StateStoreExtensions.EncodeKey("baz");

        public static KeyBytes KeyQux => StateStoreExtensions.EncodeKey("qux");

        public static KeyBytes KeyQuux => StateStoreExtensions.EncodeKey("quux");

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetStateRoot(bool secure)
        {
            var stateStore = new TrieStateStore(_stateKeyValueStore, secure);
            ITrie empty = stateStore.GetStateRoot(null);
            Assert.True(empty.Recorded);
            Assert.Null(empty.Get(new[] { KeyFoo })[0]);
            Assert.Null(empty.Get(new[] { KeyBar })[0]);
            Assert.Null(empty.Get(new[] { KeyBaz })[0]);
            Assert.Null(empty.Get(new[] { KeyQux })[0]);
            Assert.Null(empty.Get(new[] { KeyQuux })[0]);

            KeyBytes fooKey = StateStoreExtensions.EncodeKey("foo");
            KeyBytes barKey = StateStoreExtensions.EncodeKey("bar");
            KeyBytes bazKey = StateStoreExtensions.EncodeKey("baz");
            KeyBytes quxKey = StateStoreExtensions.EncodeKey("qux");
            var values = ImmutableDictionary<KeyBytes, IValue>.Empty
                .Add(fooKey, (Binary)GetRandomBytes(32))
                .Add(barKey, (Text)ByteUtil.Hex(GetRandomBytes(32)))
                .Add(bazKey, (Bencodex.Types.Boolean)false)
                .Add(quxKey, Bencodex.Types.Dictionary.Empty);
            HashDigest<SHA256> hash = stateStore.Commit(null, values).Hash;
            ITrie found = stateStore.GetStateRoot(hash);
            Assert.True(found.Recorded);
            AssertBencodexEqual(values[fooKey], found.Get(new[] { KeyFoo })[0]);
            AssertBencodexEqual(values[barKey], found.Get(new[] { KeyBar })[0]);
            AssertBencodexEqual(values[bazKey], found.Get(new[] { KeyBaz })[0]);
            AssertBencodexEqual(values[quxKey], found.Get(new[] { KeyQux })[0]);
            Assert.Null(found.Get(new[] { KeyQuux })[0]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PruneStates(bool secure)
        {
            var values = ImmutableDictionary<KeyBytes, IValue>.Empty
                .Add(StateStoreExtensions.EncodeKey("foo"), (Binary)GetRandomBytes(4096))
                .Add(
                    StateStoreExtensions.EncodeKey("bar"),
                    (Text)ByteUtil.Hex(GetRandomBytes(2048)))
                .Add(StateStoreExtensions.EncodeKey("baz"), (Bencodex.Types.Boolean)false)
                .Add(StateStoreExtensions.EncodeKey("qux"), Bencodex.Types.Dictionary.Empty)
                .Add(
                    StateStoreExtensions.EncodeKey("zzz"),
                    Bencodex.Types.Dictionary.Empty
                        .Add("binary", GetRandomBytes(4096))
                        .Add("text", ByteUtil.Hex(GetRandomBytes(2048))));

            var stateStore = new TrieStateStore(_stateKeyValueStore, secure);
            ITrie first = stateStore.Commit(null, values);

            int prevStatesCount = _stateKeyValueStore.ListKeys().Count();
            ImmutableDictionary<KeyBytes, IValue> nextStates =
                values.SetItem(StateStoreExtensions.EncodeKey("foo"), (Binary)GetRandomBytes(4096));
            ITrie second = stateStore.Commit(first.Hash, nextStates);

            // foo = 0x666f6f
            // updated branch node (0x6, aka root) + updated branch node (0x66) +
            // updated short node + new value nodes
            Assert.Equal(prevStatesCount + 4, _stateKeyValueStore.ListKeys().Count());

            stateStore.PruneStates(ImmutableHashSet<HashDigest<SHA256>>.Empty.Add(second.Hash));

            // It will stay at the same count of nodes.
            // FIXME: Bencodex fingerprints also should be tracked.
            //        https://github.com/planetarium/libplanet/issues/1653
            Assert.Equal(prevStatesCount, _stateKeyValueStore.ListKeys().Count());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CopyStates(bool secure)
        {
            var values = ImmutableDictionary<KeyBytes, IValue>.Empty
                .Add(StateStoreExtensions.EncodeKey("foo"), (Binary)GetRandomBytes(4096))
                .Add(
                    StateStoreExtensions.EncodeKey("bar"),
                    (Text)ByteUtil.Hex(GetRandomBytes(2048)))
                .Add(StateStoreExtensions.EncodeKey("baz"), (Bencodex.Types.Boolean)false)
                .Add(StateStoreExtensions.EncodeKey("qux"), Bencodex.Types.Dictionary.Empty)
                .Add(
                    StateStoreExtensions.EncodeKey("zzz"),
                    Bencodex.Types.Dictionary.Empty
                        .Add("binary", GetRandomBytes(4096))
                        .Add("text", ByteUtil.Hex(GetRandomBytes(2048))));

            var stateStore = new TrieStateStore(_stateKeyValueStore, secure);

            IKeyValueStore targetStateKeyValueStore = new MemoryKeyValueStore();
            var targetStateStore = new TrieStateStore(targetStateKeyValueStore, secure);
            ITrie trie = stateStore.Commit(null, values);
            int prevStatesCount = _stateKeyValueStore.ListKeys().Count();

            _stateKeyValueStore.Set(
                new KeyBytes("alpha", Encoding.UTF8),
                ByteUtil.ParseHex("00"));
            _stateKeyValueStore.Set(
                new KeyBytes("beta", Encoding.UTF8),
                ByteUtil.ParseHex("00"));

            Assert.Equal(prevStatesCount + 2, _stateKeyValueStore.ListKeys().Count());
            Assert.Empty(targetStateKeyValueStore.ListKeys());

            stateStore.CopyStates(
                ImmutableHashSet<HashDigest<SHA256>>.Empty.Add(trie.Hash),
                targetStateStore);

            // It will stay at the same count of nodes.
            // FIXME: Bencodex fingerprints also should be tracked.
            //        https://github.com/planetarium/libplanet/issues/1653
            Assert.Equal(prevStatesCount, targetStateKeyValueStore.ListKeys().Count());
        }

        [Fact]
#pragma warning disable S2699 // Tests should include assertions
        public void IdempotentDispose()
#pragma warning restore S2699 // Tests should include assertions
        {
            var stateStore = new TrieStateStore(_stateKeyValueStore);
            stateStore.Dispose();
#pragma warning disable S3966 // Objects should not be disposed more than once
            stateStore.Dispose();
#pragma warning restore S3966 // Objects should not be disposed more than once
        }
    }
}
