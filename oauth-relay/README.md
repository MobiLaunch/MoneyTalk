# MoneyTalk OAuth relay

Square and QuickBooks Online both require an OAuth redirect URL that's a real, publicly
reachable HTTPS endpoint, registered in advance in their developer dashboards. Neither accepts a
custom URI scheme like `moneytalk://oauth/square`, and QuickBooks only allows `http://localhost`
for sandbox testing — not production. Since MoneyTalk is a desktop app with no HTTPS endpoint of
its own, this tiny Cloudflare Worker exists to be that landing page.

All it does: read the `code` (and, for QuickBooks, `realmId`) query-string values Square/Intuit
attach to the redirect, and display them on a simple page so you can copy them back into
MoneyTalk's Integrations settings — matching the "paste the authorization code back" flow already
built into the app. It never sees a client secret and never talks to Square's or Intuit's APIs;
the actual code-for-token exchange still happens from the desktop app itself.

## Deploy it

You need a (free) Cloudflare account and the `wrangler` CLI.

```bash
cd oauth-relay
npm install -g wrangler   # if you don't already have it
wrangler login
wrangler deploy
```

`wrangler deploy` prints the worker's URL when it finishes, something like:

```
https://moneytalk-oauth-relay.<your-subdomain>.workers.dev
```

That's your relay's base URL. It's static infrastructure — deploy it once and forget about it;
there's no state, no secrets, and no ongoing cost on Cloudflare's free tier.

## Wire it up

1. **Square Developer Dashboard** (developer.squareup.com/apps → your app → OAuth):
   set the Redirect URL to `<your relay URL>/square/callback`.
2. **Intuit Developer Portal** (developer.intuit.com → your app → Keys & OAuth):
   add `<your relay URL>/quickbooks/callback` as a Redirect URI.
3. **MoneyTalk → Settings → Integrations**: paste the same two URLs into the new "Redirect URL"
   field under Square and QuickBooks respectively, then Save App Credentials. This must match
   exactly what you registered in steps 1–2, or the provider will reject the exchange.

From there, "Open Square/QuickBooks Authorization" sends the browser to Square/Intuit's consent
screen as before; once approved, it now lands on this relay instead of an unreachable
`moneytalk://` link, and you copy the code it displays into MoneyTalk the same way you already do.

## Optional: custom domain

By default the worker is served from a `*.workers.dev` subdomain, which is fine. If you'd rather
use your own domain, add a route in the Cloudflare dashboard (or a `routes` entry in
`wrangler.toml`) and use that domain in the two dashboards and in MoneyTalk's settings instead.
