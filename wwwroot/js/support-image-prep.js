/**
 * Prepare support-chat images for upload: decode, downscale, JPEG-compress,
 * and force a safe .jpg filename so mobile gallery/camera picks succeed.
 */
(function (window) {
  'use strict';

  var MAX_EDGE = 1600;
  var TARGET_BYTES = 900 * 1024;
  var HARD_MAX_BYTES = 3 * 1024 * 1024;

  function ensureJpegFile(blob) {
    var name = 'support-' + Date.now() + '.jpg';
    try {
      return new File([blob], name, { type: 'image/jpeg', lastModified: Date.now() });
    } catch (e) {
      blob.name = name;
      return blob;
    }
  }

  function loadImageFromFile(file) {
    return new Promise(function (resolve, reject) {
      var url;
      try {
        url = URL.createObjectURL(file);
      } catch (e) {
        reject(e);
        return;
      }
      var img = new Image();
      img.onload = function () {
        URL.revokeObjectURL(url);
        resolve(img);
      };
      img.onerror = function () {
        URL.revokeObjectURL(url);
        reject(new Error('decode failed'));
      };
      img.src = url;
    });
  }

  function canvasToJpegBlob(canvas, quality) {
    return new Promise(function (resolve, reject) {
      if (canvas.toBlob) {
        canvas.toBlob(function (blob) {
          if (blob) resolve(blob);
          else reject(new Error('encode failed'));
        }, 'image/jpeg', quality);
        return;
      }
      try {
        var dataUrl = canvas.toDataURL('image/jpeg', quality);
        var bin = atob(dataUrl.split(',')[1]);
        var arr = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
        resolve(new Blob([arr], { type: 'image/jpeg' }));
      } catch (e) {
        reject(e);
      }
    });
  }

  function drawScaled(img, maxEdge) {
    var w = img.naturalWidth || img.width;
    var h = img.naturalHeight || img.height;
    if (!w || !h) throw new Error('invalid image');
    var scale = Math.min(1, maxEdge / Math.max(w, h));
    var cw = Math.max(1, Math.round(w * scale));
    var ch = Math.max(1, Math.round(h * scale));
    var canvas = document.createElement('canvas');
    canvas.width = cw;
    canvas.height = ch;
    var ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('canvas unsupported');
    // White fill so transparent PNGs become valid JPEG
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, cw, ch);
    ctx.drawImage(img, 0, 0, cw, ch);
    return { canvas: canvas, width: cw, height: ch };
  }

  function hasAllowedExt(name) {
    return /\.(jpe?g|png|webp|gif)$/i.test(name || '');
  }

  function guessExtFromType(type) {
    if (!type) return '';
    if (type === 'image/jpeg' || type === 'image/jpg') return '.jpg';
    if (type === 'image/png') return '.png';
    if (type === 'image/webp') return '.webp';
    if (type === 'image/gif') return '.gif';
    return '';
  }

  function renameWithExt(file, ext) {
    var safeName = 'support-' + Date.now() + ext;
    try {
      return new File([file], safeName, {
        type: file.type || 'application/octet-stream',
        lastModified: Date.now()
      });
    } catch (e) {
      file.name = safeName;
      return file;
    }
  }

  function fallbackOriginal(file) {
    if (!file || !file.size || file.size > HARD_MAX_BYTES) {
      return Promise.reject(new Error('image too large'));
    }
    if (hasAllowedExt(file.name)) return Promise.resolve(file);
    var ext = guessExtFromType(file.type);
    if (ext) return Promise.resolve(renameWithExt(file, ext));
    return Promise.reject(new Error('unsupported image'));
  }

  /**
   * @param {File|Blob} file
   * @returns {Promise<File|Blob>}
   */
  function prepareSupportImage(file) {
    if (!file) return Promise.reject(new Error('no file'));

    return loadImageFromFile(file).then(function (img) {
      var edge = MAX_EDGE;
      var qualities = [0.82, 0.72, 0.6, 0.48, 0.36];
      var best = null;

      function tryPass() {
        var drawn = drawScaled(img, edge);
        var qi = 0;

        function nextQ() {
          if (qi >= qualities.length) {
            if (best && best.size <= HARD_MAX_BYTES) {
              return ensureJpegFile(best);
            }
            if (edge > 640) {
              edge = Math.max(640, Math.round(edge * 0.7));
              return tryPass();
            }
            throw new Error('image too large after compress');
          }
          var q = qualities[qi++];
          return canvasToJpegBlob(drawn.canvas, q).then(function (blob) {
            if (!best || blob.size < best.size) best = blob;
            if (blob.size <= TARGET_BYTES) {
              return ensureJpegFile(blob);
            }
            return nextQ();
          });
        }

        return nextQ();
      }

      return tryPass();
    }).catch(function () {
      return fallbackOriginal(file);
    });
  }

  window.__ryPrepareSupportImage = prepareSupportImage;
})(window);
