// Clear to Land - persistent int storage for WebGL players (called from PrefsStore.cs via [DllImport("__Internal")]).
//
// Why: Unity keeps WebGL PlayerPrefs in IndexedDB under /idbfs/<hash of the page URL>, and itch.io serves every
// uploaded build from a new URL, so PlayerPrefs alone resets best scores and settings on each update.
// window.localStorage is keyed by origin only, so values stored here survive new uploads. All itch.io HTML games
// share the itch.zone origin, hence the fixed game-unique key prefix.
//
// Storage can be unavailable or throw (private mode, blocked third-party storage in the itch.io iframe, quota),
// so every access is wrapped in try/catch. This session's writes are also kept in an in-memory map that takes
// priority over localStorage, so reads stay consistent even when a write could not be persisted.
// PrefsStore writes PlayerPrefs as a second copy, so blocked localStorage degrades to the old behaviour.
//
// All exported functions take a UTF-8 key pointer and return ints (no bool marshalling across the boundary).

var CtlStorageLib = {
  $CtlStorage: {
    prefix: 'cleartoland.',
    memory: {},          // prefixed key -> string value written this session, or null when deleted
    resolved: false,
    storage: null,       // window.localStorage, or null when it cannot be used

    getStorage: function () {
      if (!CtlStorage.resolved) {
        CtlStorage.resolved = true;
        try {
          if (typeof window !== 'undefined' && window.localStorage) CtlStorage.storage = window.localStorage;
        } catch (e) {
          CtlStorage.storage = null;
        }
      }
      return CtlStorage.storage;
    },

    // Returns the stored string, or null when the key is missing or storage cannot be read.
    read: function (fullKey) {
      if (Object.prototype.hasOwnProperty.call(CtlStorage.memory, fullKey)) return CtlStorage.memory[fullKey];
      var storage = CtlStorage.getStorage();
      if (!storage) return null;
      try {
        var value = storage.getItem(fullKey);
        return value === undefined ? null : value;
      } catch (e) {
        return null;
      }
    }
  },

  CtlStorageHasKey__deps: ['$CtlStorage', '$UTF8ToString'],
  CtlStorageHasKey: function (keyPtr) {
    return CtlStorage.read(CtlStorage.prefix + UTF8ToString(keyPtr)) === null ? 0 : 1;
  },

  // Returns the stored int, or defaultValue when the key is missing or does not hold an integer.
  CtlStorageGetInt__deps: ['$CtlStorage', '$UTF8ToString'],
  CtlStorageGetInt: function (keyPtr, defaultValue) {
    var value = CtlStorage.read(CtlStorage.prefix + UTF8ToString(keyPtr));
    if (value === null) return defaultValue;
    var parsed = parseInt(value, 10);
    return isNaN(parsed) ? defaultValue : (parsed | 0);
  },

  // Returns 1 when the value reached localStorage, 0 when it is only kept in memory for this session.
  // When the write fails (e.g. quota of the itch.zone origin, shared by every itch.io HTML game, is full), any
  // older persisted value is removed so it cannot outrank the newer PlayerPrefs copy in a later session.
  CtlStorageSetInt__deps: ['$CtlStorage', '$UTF8ToString'],
  CtlStorageSetInt: function (keyPtr, value) {
    var fullKey = CtlStorage.prefix + UTF8ToString(keyPtr);
    var text = String(value | 0);
    CtlStorage.memory[fullKey] = text;
    var storage = CtlStorage.getStorage();
    if (!storage) return 0;
    try {
      storage.setItem(fullKey, text);
      return 1;
    } catch (e) {
      try {
        storage.removeItem(fullKey);
      } catch (e2) {
        // Nothing else to do: PlayerPrefs still holds the value.
      }
      return 0;
    }
  },

  // Returns 1 when the key was removed from localStorage, 0 when it is only hidden for this session.
  CtlStorageDeleteKey__deps: ['$CtlStorage', '$UTF8ToString'],
  CtlStorageDeleteKey: function (keyPtr) {
    var fullKey = CtlStorage.prefix + UTF8ToString(keyPtr);
    CtlStorage.memory[fullKey] = null;
    var storage = CtlStorage.getStorage();
    if (!storage) return 0;
    try {
      storage.removeItem(fullKey);
      return 1;
    } catch (e) {
      return 0;
    }
  }
};

mergeInto(LibraryManager.library, CtlStorageLib);
