﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TableStorage.Abstractions.Trie
{
	/// <summary>
	/// Manages multiple indexes using a trie-like strategy to accomplish "begins with" searching in Azure Table Storage.
	/// <example>Imagine a type-ahead for customers where you can search by full name, last name, and email.  This class will manage all 3 indexes to accomplish this goal.</example>
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class MultiIndexTrieSearch<T> where T : class, new()
	{
		private readonly IDictionary<ITrieSearch<T>, Func<T, string>> _indexes;

		public MultiIndexTrieSearch(IDictionary<ITrieSearch<T>, Func<T, string>> indexes)
		{
			_indexes = indexes ?? throw new ArgumentNullException(nameof(indexes));
		}

		public Task DropIndexAsync()
		{
			var tasks = new Task[_indexes.Count];

			for (var i = 0; i < tasks.Length; i++)
			{
				var trieSearch = _indexes.ElementAt(i).Key;
				tasks[i] = trieSearch.DropIndexAsync();
			}

			return Task.WhenAll(tasks);
		}

		public Task IndexAsync(T data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			var tasks = new Task[_indexes.Count];

			for (var i = 0; i < tasks.Length; i++)
			{
				var kvp = _indexes.ElementAt(i);
				var trieSearch = kvp.Key;
				var getIndex = kvp.Value;
				var index = getIndex(data);
				tasks[i] = trieSearch.IndexAsync(data, index);
			}

			return Task.WhenAll(tasks);
		}

		public Task DeleteAsync(T data)
		{
			if (data == null)
				throw new ArgumentNullException(nameof(data));

			var tasks = new Task[_indexes.Count];

			for (var i = 0; i < tasks.Length; i++)
			{
				var kvp = _indexes.ElementAt(i);
				var trieSearch = kvp.Key;
				var getIndex = kvp.Value;
				var index = getIndex(data);
				tasks[i] = trieSearch.DeleteAsync(index, data);
			}

			return Task.WhenAll(tasks);
		}

		public Task ReindexAsync(T oldData, T newData)
		{
			if (oldData == null)
				throw new ArgumentNullException(nameof(oldData));

			if (newData == null)
				throw new ArgumentNullException(nameof(newData));

			var tasks = new Task[_indexes.Count];

			for (var i = 0; i < tasks.Length; i++)
			{
				var kvp = _indexes.ElementAt(i);
				var trieSearch = kvp.Key;
				var getIndex = kvp.Value;
				var newIndex = getIndex(newData);
				var oldIndex = getIndex(oldData);
				tasks[i] = trieSearch.ReindexAsync(newData, oldIndex, newIndex);
			}

			return Task.WhenAll(tasks);
		}

		public async Task<IEnumerable<T>> FindAsync(string term, Func<IEnumerable<T>, IEnumerable<T>> dedupe,
			int pageSize = 10)
		{
			if (string.IsNullOrEmpty(term))
				throw new ArgumentException(nameof(term));

			if (dedupe == null)
				throw new ArgumentNullException(nameof(dedupe));

			if (pageSize < 0)
				throw new ArgumentException(nameof(pageSize));

			var tasks = new Task<IEnumerable<T>>[_indexes.Count];

			for (var i = 0; i < tasks.Length; i++)
			{
				var kvp = _indexes.ElementAt(i);
				var trieSearch = kvp.Key;
				tasks[i] = trieSearch.FindAsync(term, pageSize);
			}

			await Task.WhenAll(tasks).ConfigureAwait(false);

			var results = new List<T>();
			foreach (var task in tasks) results.AddRange(task.Result);

			return dedupe(results).Take(pageSize);
		}
	}
}