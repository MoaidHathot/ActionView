import { useRef, useState, type ReactNode } from 'react';
import type { ContentBlock, VideoChapter } from '../../types';
import { rewriteImageUrl } from '../../utils/imageUrl';

interface Props {
  block: ContentBlock;
}

/**
 * Embeds a video. Three providers are supported:
 *
 *   - "youtube":  iframe to youtube-nocookie.com with optional start/end clipping
 *   - "vimeo":    iframe to vimeo.com player
 *   - "file":     native <video> element. URL can be http(s) or file:// (rewritten via /api/files)
 *
 * If no provider is set the renderer guesses from the URL: youtube.com / youtu.be
 * -> youtube; vimeo.com -> vimeo; anything else -> file.
 *
 * Optional `chapters[]` renders a timestamp list beneath the player. For
 * file videos, clicking a chapter seeks the <video>; for iframe providers
 * it reloads the iframe with the new start time.
 */
export function VideoBlock({ block }: Props) {
  const rawUrl = block.url ?? (typeof block.body === 'string' ? block.body : '');
  const provider = block.provider ?? guessProvider(rawUrl);
  const videoRef = useRef<HTMLVideoElement>(null);

  const [iframeStart, setIframeStart] = useState<number | undefined>(block.startTime);

  const seek = (seconds: number) => {
    if (provider === 'file' && videoRef.current) {
      videoRef.current.currentTime = seconds;
      void videoRef.current.play();
    } else {
      setIframeStart(seconds);
    }
  };

  let player: ReactNode = null;
  if (provider === 'youtube') {
    const id = block.videoId ?? extractYouTubeId(rawUrl);
    if (id) {
      const params = new URLSearchParams();
      params.set('rel', '0');
      params.set('modestbranding', '1');
      if (iframeStart !== undefined) params.set('start', String(Math.floor(iframeStart)));
      if (block.endTime !== undefined) params.set('end', String(Math.floor(block.endTime)));
      const src = `https://www.youtube-nocookie.com/embed/${encodeURIComponent(id)}?${params}`;
      player = <iframe className="video-iframe" src={src} title={block.label ?? 'YouTube video'} allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowFullScreen />;
    }
  } else if (provider === 'vimeo') {
    const id = block.videoId ?? extractVimeoId(rawUrl);
    if (id) {
      const params = new URLSearchParams();
      if (iframeStart !== undefined) params.set('#t', `${Math.floor(iframeStart)}s`);
      const src = `https://player.vimeo.com/video/${encodeURIComponent(id)}?${params}`;
      player = <iframe className="video-iframe" src={src} title={block.label ?? 'Vimeo video'} allow="autoplay; fullscreen; picture-in-picture" allowFullScreen />;
    }
  } else {
    // file
    const src = rewriteImageUrl(rawUrl);
    if (src) {
      player = (
        <video
          ref={videoRef}
          className="video-element"
          src={src + (block.startTime ? `#t=${block.startTime}${block.endTime ? `,${block.endTime}` : ''}` : '')}
          controls
          poster={block.poster ? rewriteImageUrl(block.poster) : undefined}
          preload="metadata"
        />
      );
    }
  }

  if (!player) {
    return (
      <div className="block-video block-video-missing">
        <div className="block-video-missing-msg">Video block has no recognizable URL/provider.</div>
      </div>
    );
  }

  return (
    <div className="block-video">
      {block.label && <h4 className="block-label">{block.label}</h4>}
      <div className="video-frame">{player}</div>
      {block.caption && <div className="video-caption">{block.caption}</div>}
      {block.chapters && block.chapters.length > 0 && (
        <ChaptersList chapters={block.chapters} onJump={seek} />
      )}
    </div>
  );
}

function ChaptersList({ chapters, onJump }: { chapters: VideoChapter[]; onJump: (s: number) => void }) {
  return (
    <ol className="video-chapters">
      {chapters.map((ch, i) => (
        <li key={i}>
          <button
            type="button"
            className="video-chapter-btn"
            onClick={() => onJump(ch.at)}
            title={`Jump to ${formatTime(ch.at)}`}
          >
            <span className="video-chapter-time">{formatTime(ch.at)}</span>
            <span className="video-chapter-label">{ch.label}</span>
          </button>
        </li>
      ))}
    </ol>
  );
}

function guessProvider(url: string): 'youtube' | 'vimeo' | 'file' {
  if (/youtube\.com|youtu\.be/i.test(url)) return 'youtube';
  if (/vimeo\.com/i.test(url)) return 'vimeo';
  return 'file';
}

function extractYouTubeId(url: string): string | null {
  try {
    const u = new URL(url);
    if (u.hostname === 'youtu.be') return u.pathname.slice(1) || null;
    if (u.hostname.includes('youtube.com')) {
      const v = u.searchParams.get('v');
      if (v) return v;
      // /embed/<id>, /shorts/<id>
      const parts = u.pathname.split('/').filter(Boolean);
      if (parts.length >= 2 && (parts[0] === 'embed' || parts[0] === 'shorts')) return parts[1];
    }
  } catch { /* not parseable */ }
  return null;
}

function extractVimeoId(url: string): string | null {
  try {
    const u = new URL(url);
    if (u.hostname.includes('vimeo.com')) {
      const m = u.pathname.match(/(\d+)/);
      return m ? m[1] : null;
    }
  } catch { /* not parseable */ }
  return null;
}

function formatTime(sec: number): string {
  const s = Math.max(0, Math.floor(sec));
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const ss = s % 60;
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(ss).padStart(2, '0')}`;
  return `${m}:${String(ss).padStart(2, '0')}`;
}
