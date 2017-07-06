﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace pbXNet
{
	public class FileSystemInDatabase : IFileSystem, IDisposable
	{
		class FsEntry
		{
			public string Path { get; set; }
			public string Name { get; set; }
			public bool IsDirectory { get; set; }
			public string Data { get; set; }
			public DateTime CreatedOn { get; set; }
			public DateTime ModifiedOn { get; set; }
		}

		public FileSystemType Type { get; private set; }

		/// <summary>
		/// The file system identifier, and the table name in the database.
		/// </summary>
		public string Id { get; }

		public string Name => _db?.Name;

		public string RootPath { get; protected set; }

		public string CurrentPath { get; protected set; }

		Stack<string> _visitedPaths = new Stack<string>();

		ISimpleDatabase _db;

		FsEntry _pk = new FsEntry();

		protected FileSystemInDatabase(ISimpleDatabase db, string id)
		{
			Id = id ?? throw new ArgumentNullException(nameof(id));
			_db = db ?? throw new ArgumentNullException(nameof(db));
			RootPath = CurrentPath = "/";
		}

		public static async Task<IFileSystem> NewAsync(ISimpleDatabase db, string id)
		{
			if (db == null)
				throw new ArgumentNullException(nameof(db));
			if (id == null)
				throw new ArgumentNullException(nameof(id));

			var fs = new FileSystemInDatabase(db, id);
			await fs.InitializeAsync();
			return fs;
		}

		public virtual void Initialize()
		{
			InitializeAsync().GetAwaiter().GetResult();
		}

		public virtual async Task InitializeAsync()
		{
			await _db.InitializeAsync().ConfigureAwait(false);

			FsEntry entry;
			await _db.CreateTableAsync<FsEntry>(Id).ConfigureAwait(false);
			await _db.CreatePrimaryKeyAsync<FsEntry>(Id, nameof(entry.Path), nameof(entry.Name)).ConfigureAwait(false);
			await _db.CreateIndexAsync<FsEntry>(Id, false, nameof(entry.Path)).ConfigureAwait(false);
		}

		public void Dispose()
		{
			_visitedPaths.Clear();
			RootPath = CurrentPath = null;
		}

		public async Task<IFileSystem> CloneAsync()
		{
			FileSystemInDatabase fs = new FileSystemInDatabase(_db, Id)
			{
				RootPath = this.RootPath,
				CurrentPath = this.CurrentPath,
				_visitedPaths = new Stack<string>(_visitedPaths.AsEnumerable()),
			};
			return fs;
		}

		DeviceFileSystem.State _state = new DeviceFileSystem.State();

		public virtual Task SaveStateAsync()
		{
			_state.Save(RootPath, CurrentPath, _visitedPaths);
			return Task.FromResult(true);
		}

		public virtual Task RestoreStateAsync()
		{
			string rootPath = "", currentPath = "";
			if (_state.Restore(ref rootPath, ref currentPath, ref _visitedPaths))
			{
				RootPath = rootPath;
				CurrentPath = currentPath;
			}
			return Task.FromResult(true);
		}

		public async Task SetCurrentDirectoryAsync(string dirname)
		{
			if (string.IsNullOrEmpty(dirname))
			{
				CurrentPath = RootPath;
				_visitedPaths.Clear();
			}
			else if (dirname == "..")
			{
				CurrentPath = _visitedPaths.Count > 0 ? _visitedPaths.Pop() : RootPath;
			}
			else
			{
				if (!await DirectoryExistsAsync(dirname).ConfigureAwait(false))
					throw new DirectoryNotFoundException(T.Localized("FS_DirNotFound", CurrentPath, dirname));

				_visitedPaths.Push(CurrentPath);
				CurrentPath = GetFilePath(dirname);
			}
		}

		public async Task<IEnumerable<string>> GetDirectoriesAsync(string pattern = "")
		{
			var l = await _db.SelectAsync<FsEntry>(Id, (e) => e.Path == CurrentPath).ConfigureAwait(false);
			return l.Where((e) => e.IsDirectory && Regex.IsMatch(e.Name, pattern)).Select((e) => e.Name);
		}

		public async Task<bool> DirectoryExistsAsync(string dirname)
		{
			if (string.IsNullOrEmpty(dirname))
				return false;

			_pk.Path = CurrentPath;
			_pk.Name = dirname;
			return await _db.FindAsync(Id, _pk).ConfigureAwait(false) != null;
		}

		public async Task CreateDirectoryAsync(string dirname)
		{
			if (string.IsNullOrEmpty(dirname))
				throw new ArgumentNullException(nameof(dirname));

			if (!await DirectoryExistsAsync(dirname).ConfigureAwait(false))
			{
				await _db.InsertAsync(Id,
					new FsEntry
					{
						Path = CurrentPath,
						Name = dirname,
						IsDirectory = true,
						CreatedOn = DateTime.UtcNow,
						ModifiedOn = DateTime.UtcNow,
					})
					.ConfigureAwait(false);
			}

			_visitedPaths.Push(CurrentPath);
			CurrentPath = GetFilePath(dirname);
		}

		public async Task DeleteDirectoryAsync(string dirname)
		{
			if (string.IsNullOrEmpty(dirname))
				throw new ArgumentNullException(nameof(dirname));

			if (!await DirectoryExistsAsync(dirname).ConfigureAwait(false))
				return;

			var l = await _db.SelectAsync<FsEntry>(Id, (e) => e.Path == GetFilePath(dirname)).ConfigureAwait(false);
			if (l.Any())
				throw new IOException(T.Localized("FS_DirNotEmpty", CurrentPath, dirname));

			_pk.Path = CurrentPath;
			_pk.Name = dirname;
			await _db.DeleteAsync(Id, _pk).ConfigureAwait(false);
		}

		public async Task<IEnumerable<string>> GetFilesAsync(string pattern = "")
		{
			var l = await _db.SelectAsync<FsEntry>(Id, (e) => e.Path == CurrentPath).ConfigureAwait(false);
			return l.Where((e) => !e.IsDirectory && Regex.IsMatch(e.Name, pattern)).Select((e) => e.Name);
		}

		async Task<FsEntry> GetFsEntryAsync(string filename)
		{
			_pk.Path = CurrentPath;
			_pk.Name = filename;
			return await _db.FindAsync(Id, _pk).ConfigureAwait(false);
		}

		public async Task<bool> FileExistsAsync(string filename)
		{
			if (string.IsNullOrEmpty(filename))
				return false;

			return await GetFsEntryAsync(filename).ConfigureAwait(false) != null;
		}

		public async Task DeleteFileAsync(string filename)
		{
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentNullException(nameof(filename));

			if (!await FileExistsAsync(filename).ConfigureAwait(false))
				return;

			_pk.Path = CurrentPath;
			_pk.Name = filename;
			await _db.DeleteAsync(Id, _pk).ConfigureAwait(false);
		}

		public async Task SetFileModifiedOnAsync(string filename, DateTime modifiedOn)
		{
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentNullException(nameof(filename));

			FsEntry e = await GetFsEntryAsync(filename).ConfigureAwait(false);
			if (e == null)
				throw new FileNotFoundException(T.Localized("FS_FileNotFound", CurrentPath, filename));

			e.ModifiedOn = modifiedOn.ToUniversalTime();
			await _db.UpdateAsync(Id, e).ConfigureAwait(false);
		}

		public async Task<DateTime> GetFileModifiedOnAsync(string filename)
		{
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentNullException(nameof(filename));

			FsEntry e = await GetFsEntryAsync(filename).ConfigureAwait(false);
			if (e == null)
				throw new FileNotFoundException(T.Localized("FS_FileNotFound", CurrentPath, filename));

			return e.ModifiedOn;
		}

		public async Task WriteTextAsync(string filename, string text)
		{
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentNullException(nameof(filename));

			await _db.InsertAsync(Id,
				new FsEntry
				{
					Path = CurrentPath,
					Name = filename,
					IsDirectory = false,
					Data = text,
					CreatedOn = DateTime.UtcNow,
					ModifiedOn = DateTime.UtcNow,
				})
				.ConfigureAwait(false);
		}

		public async Task<string> ReadTextAsync(string filename)
		{
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentNullException(nameof(filename));

			FsEntry e = await GetFsEntryAsync(filename).ConfigureAwait(false);
			if (e == null)
				throw new FileNotFoundException(T.Localized("FS_FileNotFound", CurrentPath, filename));

			return e.Data;
		}

		#region Tools

		protected virtual string GetFilePath(string filename)
		{
			return Path.Combine(CurrentPath, filename).Replace(Path.DirectorySeparatorChar, '/');
		}

		#endregion
	}
}