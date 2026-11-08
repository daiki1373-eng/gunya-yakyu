// ブラウザ版(特に iPhone の Safari)で文字を入れるための入口。
// Unity の入力欄はスマホのキーボードが出ないことがあるので、ブラウザ標準の入力ダイアログを使う。
mergeInto(LibraryManager.library, {
  WebPromptJs: function (titlePtr, defPtr) {
    var r = window.prompt(UTF8ToString(titlePtr), UTF8ToString(defPtr));
    if (r === null || r === undefined) r = "\u001bCANCEL";
    var size = lengthBytesUTF8(r) + 1;
    var buf = _malloc(size);
    stringToUTF8(r, buf, size);
    return buf;
  }
});
