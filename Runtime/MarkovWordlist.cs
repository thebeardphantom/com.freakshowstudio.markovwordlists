using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using Random = System.Random;

namespace FreakshowStudio.MarkovWordLists.Runtime
{
    public class MarkovWordlist : ScriptableObject, ISerializationCallbackReceiver
    {
        #region Types

        [Serializable]
        private struct ChainElement
        {
            #region Properties

            [field: SerializeField]
            public string Key { get; set; }

            [field: SerializeField]
            public string Value { get; set; }

            [field: SerializeField]
            public double Probability { get; set; }

            #endregion
        }

        #endregion

        #region Fields

        private const char START_CHARACTER = (char)2;

        private const char END_CHARACTER = (char)3;

        private static readonly string _startCharacterStr = START_CHARACTER.ToString();

        private static readonly string _endCharacterStr = END_CHARACTER.ToString();

        private static readonly StringBuilder _stringBuilder = new();

        private Dictionary<string, Dictionary<string, double>> _chain = new();

        #endregion

        #region Properties

        [field: Min(1)]
        [field: SerializeField]
        private int Order { get; set; }

        [field: HideInInspector]
        [field: SerializeField]
        [SuppressMessage("ReSharper", "Unity.RedundantHideInInspectorAttribute")]
        private ChainElement[] SerializedChain { get; set; }

        #endregion

        #region Methods

        public static MarkovWordlist FromData(int order, IEnumerable<string> words)
        {
            Assert.IsTrue(order > 0, "order > 0");
            var asset = CreateInstance<MarkovWordlist>();
            asset.Order = order;
            asset.GenerateChain(words);
            return asset;
        }

        private static Dictionary<string, Dictionary<string, double>> NormalizeChain(
            Dictionary<string, Dictionary<string, int>> chain)
        {
            var normalizedChain = new Dictionary<string, Dictionary<string, double>>();
            foreach ((var key, var row) in chain)
            {
                var sum = 0;
                foreach (var value in row.Values)
                {
                    sum += value;
                }

                normalizedChain[key] = row.ToDictionary(pair => pair.Key, pair => (double)pair.Value / sum);
            }

            return normalizedChain;
        }

        public string GenerateName(int lengthMin, int lengthMax)
        {
            return GenerateName(lengthMin, lengthMax, Environment.TickCount);
        }

        public string GenerateName(int lengthMin, int lengthMax, int randomSeed)
        {
            return GenerateName(lengthMin, lengthMax, new Random(randomSeed));
        }

        public string GenerateName(int lengthMin, int lengthMax, Random random)
        {
            try
            {
                var window = new string(START_CHARACTER, Order);
                _stringBuilder.Append(window);

                while (true)
                {
                    var next = PickNext(window, random);
                    _stringBuilder.Append(next);

                    var currentName = _stringBuilder.ToString();
                    var currentLength = currentName.Length;

                    var naturalLength = currentName.LastIndexOf(END_CHARACTER);
                    var trimmedName = currentName.Replace(_startCharacterStr, "").Replace(_endCharacterStr, "");
                    var trimmedLength = trimmedName.Length;
                    if (trimmedLength >= lengthMax)
                    {
                        trimmedName = trimmedName[..lengthMax];
                        return trimmedName;
                    }

                    if (naturalLength >= 0 && trimmedLength >= lengthMin)
                    {
                        return trimmedName;
                    }

                    if (naturalLength >= 0)
                    {
                        currentName = currentName[..(naturalLength - 1)];
                        currentLength = currentName.Length;
                        Assert.IsTrue(currentName.Length > 0);
                    }

                    var startIndex = currentLength - Order;
                    var length = Order;
                    if (startIndex < 0)
                    {
                        length += startIndex;
                        if (length < 0)
                        {
                            length = 1;
                        }

                        startIndex = 0;
                    }

                    window = currentName.Substring(startIndex, length);
                }
            }
            finally
            {
                _stringBuilder.Clear();
            }
        }

        public void OnBeforeSerialize()
        {
            var flattenedChain = _chain.SelectMany(
                    keys => keys.Value,
                    (keys, values) => new ChainElement
                    {
                        Key = keys.Key,
                        Value = values.Key,
                        Probability = values.Value
                    })
                .ToArray();
            SerializedChain = flattenedChain;
        }

        public void OnAfterDeserialize()
        {
            _chain = SerializedChain
                .GroupBy(e => e.Key)
                .ToDictionary(g => g.Key, g => g.ToDictionary(d => d.Value, d => d.Probability));
        }

        private string PickNext(string key, Random random)
        {
            var hasKey = _chain.ContainsKey(key);
            while (!hasKey)
            {
                if (key.Length == 1)
                {
                    return _endCharacterStr;
                }

                key = key.Substring(1, key.Length - 1);
                hasKey = _chain.ContainsKey(key);
            }

            var row = _chain[key];
            var r = random.NextDouble();
            var n = 0.0;

            foreach (var kvp in row)
            {
                n += kvp.Value;
                if (r < n)
                {
                    return kvp.Key;
                }
            }

            return row.Last().Key;
        }

        private void GenerateChain(IEnumerable<string> words)
        {
            _chain.Clear();

            var chain = new Dictionary<string, Dictionary<string, int>>();

            foreach (var word in words)
            {
                var lowercaseWord = word.ToLowerInvariant();

                for (var orderIndex = 0; orderIndex < Order; ++orderIndex)
                {
                    var orderLength = orderIndex + 1;
                    var startString = new string(START_CHARACTER, orderLength);
                    var paddedWord = $"{startString}{lowercaseWord}{_endCharacterStr}";
                    var paddedWordLength = paddedWord.Length;

                    for (var wordIndex = 0; wordIndex < paddedWordLength - orderLength; wordIndex++)
                    {
                        var key = paddedWord.Substring(wordIndex, orderLength);
                        var value = paddedWord.Substring(wordIndex + orderLength, 1);

                        if (chain.ContainsKey(key))
                        {
                            if (chain[key].ContainsKey(value))
                            {
                                chain[key][value]++;
                            }
                            else
                            {
                                chain[key].Add(value, 1);
                            }
                        }
                        else
                        {
                            chain.Add(key, new Dictionary<string, int>());
                            chain[key].Add(value, 1);
                        }
                    }
                }
            }

            _chain = NormalizeChain(chain);
        }

        #endregion
    }
}