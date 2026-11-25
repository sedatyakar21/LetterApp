import 'package:flutter/material.dart';

/// Fotoğrafları tam ekran gösterir: sağa / sola kaydırarak geçiş, iki parmakla yakınlaştırma.
/// [urls] tam adres olmalı; [headers] korumalı fotoğraflar için oturum başlıkları.
Future<void> showPhotoViewer(
  BuildContext context, {
  required List<String> urls,
  List<String> titles = const [],
  int initialIndex = 0,
  Map<String, String>? headers,
}) =>
    Navigator.of(context).push(PageRouteBuilder<void>(
      opaque: false,
      barrierColor: Colors.black,
      pageBuilder: (_, _, _) => _PhotoViewer(urls: urls, titles: titles, initialIndex: initialIndex, headers: headers),
      transitionsBuilder: (_, animation, _, child) => FadeTransition(opacity: animation, child: child),
    ));

class _PhotoViewer extends StatefulWidget {
  const _PhotoViewer({required this.urls, required this.titles, required this.initialIndex, this.headers});
  final List<String> urls;
  final List<String> titles;
  final int initialIndex;
  final Map<String, String>? headers;

  @override
  State<_PhotoViewer> createState() => _PhotoViewerState();
}

class _PhotoViewerState extends State<_PhotoViewer> {
  late final _pages = PageController(initialPage: widget.initialIndex);
  late int _index = widget.initialIndex;

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final count = widget.urls.length;
    final title = _index < widget.titles.length ? widget.titles[_index] : '';
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 15)),
        actions: [
          if (count > 1)
            Center(
              child: Padding(
                padding: const EdgeInsets.only(right: 16),
                child: Text('${_index + 1} / $count', style: const TextStyle(color: Colors.white70)),
              ),
            ),
        ],
      ),
      body: PageView.builder(
        controller: _pages,
        itemCount: count,
        onPageChanged: (i) => setState(() => _index = i),
        itemBuilder: (_, i) => InteractiveViewer(
          minScale: 1,
          maxScale: 5,
          child: Center(
            child: Image.network(
              widget.urls[i],
              headers: widget.headers,
              fit: BoxFit.contain,
              loadingBuilder: (_, child, progress) =>
                  progress == null ? child : const CircularProgressIndicator(color: Colors.white),
              errorBuilder: (_, _, _) => const Icon(Icons.broken_image_outlined, color: Colors.white54, size: 56),
            ),
          ),
        ),
      ),
    );
  }
}
