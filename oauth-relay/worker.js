// MoneyTalk OAuth relay.
//
// Square and QuickBooks Online both require an OAuth redirect URL that is a real, publicly
// reachable HTTPS endpoint registered in advance in their developer dashboards — neither accepts
// a custom URI scheme (like "moneytalk://oauth/square") as that registered value, and QuickBooks
// only allows a plain "http://localhost" redirect for sandbox testing, not production. A desktop
// app has no such endpoint of its own, so this worker exists purely to be that landing page: the
// browser lands here after the user approves access, and this worker reads the "code" (and, for
// QuickBooks, "realmId") query-string values Square/Intuit attached to the redirect and displays
// them for the user to copy back into MoneyTalk's "Authorization code" field.
//
// This worker never sees or stores a client secret and never talks to Square's or Intuit's APIs
// itself — the actual code-for-token exchange still happens from the desktop app directly, using
// the Client ID/Secret the user entered into MoneyTalk's own Integrations settings page. That
// keeps this relay tiny, stateless, and safe to redeploy or throw away at any time.

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>"']/g, (ch) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[ch]));
}

function renderPage({ title, heading, bodyHtml }) {
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>${escapeHtml(title)}</title>
<style>
  :root { color-scheme: light dark; }
  body {
    font-family: -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif;
    max-width: 560px; margin: 48px auto; padding: 0 20px; line-height: 1.5;
    color: #1a1a1a; background: #f7f7f8;
  }
  @media (prefers-color-scheme: dark) { body { color: #f0f0f0; background: #17181c; } }
  .card {
    background: #fff; border-radius: 12px; padding: 28px 32px; box-shadow: 0 1px 3px rgba(0,0,0,.08);
  }
  @media (prefers-color-scheme: dark) { .card { background: #23252b; } }
  h1 { font-size: 1.3rem; margin: 0 0 4px; }
  .sub { color: #666; margin: 0 0 24px; font-size: .95rem; }
  .field { margin-bottom: 18px; }
  .field label { display: block; font-size: .8rem; font-weight: 600; text-transform: uppercase;
    letter-spacing: .03em; color: #888; margin-bottom: 6px; }
  .value-row { display: flex; gap: 8px; align-items: stretch; }
  .value {
    flex: 1; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: .95rem;
    background: #f0f0f2; border-radius: 8px; padding: 10px 12px; word-break: break-all; user-select: all;
  }
  @media (prefers-color-scheme: dark) { .value { background: #14151a; } }
  button.copy {
    border: none; border-radius: 8px; padding: 0 16px; font-size: .85rem; font-weight: 600;
    background: #2f6fed; color: #fff; cursor: pointer; white-space: nowrap;
  }
  button.copy:active { transform: translateY(1px); }
  .steps { font-size: .9rem; color: #555; margin-top: 24px; }
  @media (prefers-color-scheme: dark) { .steps { color: #aaa; } }
  .error { background: #fdecea; color: #7a1f14; border-radius: 8px; padding: 14px 16px; font-size: .9rem; }
  @media (prefers-color-scheme: dark) { .error { background: #402320; color: #ffb4a8; } }
</style>
</head>
<body>
  <div class="card">
    <h1>${escapeHtml(heading)}</h1>
    ${bodyHtml}
  </div>
  <script>
    for (const btn of document.querySelectorAll('button.copy')) {
      btn.addEventListener('click', () => {
        const target = document.getElementById(btn.dataset.target);
        navigator.clipboard.writeText(target.textContent.trim()).then(() => {
          const original = btn.textContent;
          btn.textContent = 'Copied!';
          setTimeout(() => { btn.textContent = original; }, 1500);
        });
      });
    }
  </script>
</body>
</html>`;
}

function renderField(label, id, value) {
  return `<div class="field">
    <label>${escapeHtml(label)}</label>
    <div class="value-row">
      <div class="value" id="${id}">${escapeHtml(value)}</div>
      <button class="copy" data-target="${id}">Copy</button>
    </div>
  </div>`;
}

function handleCallback(url, provider, fields) {
  const params = url.searchParams;
  const error = params.get('error');

  if (error) {
    const description = params.get('error_description') || 'No further details were provided.';
    return renderPage({
      title: `MoneyTalk — ${provider} connection failed`,
      heading: `${provider} declined the connection`,
      bodyHtml: `<p class="error"><strong>${escapeHtml(error)}</strong><br />${escapeHtml(description)}</p>
        <p class="steps">Close this tab and try "Open ${provider} Authorization" again from MoneyTalk's
        Integrations settings page.</p>`,
    });
  }

  const code = params.get('code');
  if (!code) {
    return renderPage({
      title: `MoneyTalk — ${provider} connection`,
      heading: 'No authorization code found',
      bodyHtml: `<p class="error">This page was opened without a <code>code</code> value in its address.
        If you navigated here directly, go back to MoneyTalk and click
        "Open ${escapeHtml(provider)} Authorization" instead.</p>`,
    });
  }

  const fieldsHtml = fields
    .map(({ label, id, param }) => renderField(label, id, params.get(param) || ''))
    .join('\n');

  return renderPage({
    title: `MoneyTalk — ${provider} connected`,
    heading: `${provider} approved the connection`,
    bodyHtml: `${fieldsHtml}
      <p class="steps">Copy the value(s) above and paste them into MoneyTalk's Integrations settings
      page, then click "Connect ${escapeHtml(provider)}". You can close this tab afterward.</p>`,
  });
}

export default {
  async fetch(request) {
    const url = new URL(request.url);

    if (url.pathname === '/square/callback') {
      return new Response(
        handleCallback(url, 'Square', [
          { label: 'Authorization code', id: 'code', param: 'code' },
        ]),
        { headers: { 'content-type': 'text/html; charset=utf-8' } },
      );
    }

    if (url.pathname === '/quickbooks/callback') {
      return new Response(
        handleCallback(url, 'QuickBooks', [
          { label: 'Authorization code', id: 'code', param: 'code' },
          { label: 'Realm ID (company id)', id: 'realmId', param: 'realmId' },
        ]),
        { headers: { 'content-type': 'text/html; charset=utf-8' } },
      );
    }

    if (url.pathname === '/' || url.pathname === '') {
      return new Response(
        renderPage({
          title: 'MoneyTalk OAuth relay',
          heading: 'MoneyTalk OAuth relay',
          bodyHtml: `<p class="sub">This service only exists to catch OAuth redirects from Square and
            QuickBooks and hand the authorization code back to you. It's not a page you need to visit
            directly.</p>`,
        }),
        { headers: { 'content-type': 'text/html; charset=utf-8' } },
      );
    }

    return new Response('Not found', { status: 404 });
  },
};
