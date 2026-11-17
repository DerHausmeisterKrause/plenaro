using System.Text.Json;
using System.Text.RegularExpressions;

namespace Plenaro.Api.Favorites;

/// <summary>
/// DOM autofill parity with the WPF WebShortcutBrowserSessionManager.
/// Plaintext credentials may enter the browser ONLY on a unique, dedicated
/// per-personal-favorite HTTPS hostname (never the shared /site-... host).
/// The caller must first validate user, session, origin and authorization.
/// </summary>
internal static class FavoriteDomAutoLogin
{
    internal static bool Eligible(string slug,bool autoLogin,bool pathMode,
        string? username,string host,string? preferredHost)
    {
        return autoLogin && !pathMode &&
            slug.StartsWith("site-",StringComparison.Ordinal) &&
            Guid.TryParseExact(slug.AsSpan(5),"N",out _) &&
            !string.IsNullOrWhiteSpace(username) &&
            !string.IsNullOrWhiteSpace(preferredHost) &&
            host.Equals(preferredHost,StringComparison.OrdinalIgnoreCase);
    }

    // Keeping the template separate makes it possible to execute the EXACT
    // shipped JS in the frontend's browser-runtime contract test.
    private const string AutoFillScript = """
        <script data-plenaro-dom-autofill>
        (function () {
          "use strict";
          const expectedHost = __HOST__;
          if (location.protocol !== "https:" ||
              location.hostname.toLowerCase() !== expectedHost.toLowerCase())
            return;

          const username = __USERNAME__;
          const password = __PASSWORD__;
          const attemptKey = "plenaro:autofill:" + __NONCE__;
          let submitted = false;
          try { submitted = sessionStorage.getItem(attemptKey) === "submitted"; }
          catch (_) { /* Browser storage may be disabled in a sandbox. */ }
          if (submitted) return;

          const selectors = {
            user: [
              'input[placeholder="Benutzername"]',
              'input.z-focustextbox-real[type="text"][placeholder="Benutzername"]',
              'input[autocomplete="username"]', 'input[autocomplete="email"]',
              'input[type="email"]', 'input[name="j_username"]',
              'input[name="username"]', 'input[name="user"]', 'input[name="User"]',
              'input[name="login"]', 'input[name="Login"]', 'input[name="email"]',
              'input[id*="username" i]', 'input[id*="user" i]',
              'input[id*="login" i]', 'input[id*="email" i]',
              'input[aria-label*="Benutzer" i]'
            ],
            password: [
              'input[placeholder="Kennwort"]',
              'input.z-focustextbox-real[type="password"]',
              'input[type="password"]', 'input[autocomplete="current-password"]',
              'input[name="password"]', 'input[name="Password"]',
              'input[name="passwd"]', 'input[name="j_password"]',
              'input[id*="password" i]', 'input[id*="passwd" i]'
            ]
          };

          function active(element) {
            if (!element || element.disabled || element.readOnly ||
                element.getAttribute("aria-disabled") === "true") return false;
            const style = getComputedStyle(element);
            return style.display !== "none" && style.visibility !== "hidden" &&
                element.getClientRects().length > 0;
          }
          function find(list) {
            for (const selector of list) {
              const found = Array.from(document.querySelectorAll(selector)).find(active);
              if (found) return found;
            }
            return null;
          }
          function findLoginFields() {
            const pass = find(selectors.password);
            if (!pass || pass.autocomplete === "new-password") return null;
            const form = pass.closest("form");
            if (form && /(?:reset|forgot|register|sign-?up|change.?password)/i
                .test(form.action || "")) return null;
            // Never choose a registration/password-change dialog.
            const passwords = Array.from((form || document)
                .querySelectorAll('input[type="password"]')).filter(active);
            if (passwords.length !== 1) return null;
            let user = find(selectors.user);
            if (!user) {
              const candidates = Array.from((form || document)
                  .querySelectorAll('input[type="text"],input[type="email"]'))
                  .filter(active);
              if (candidates.length === 1) user = candidates[0];
            }
            if (!user || (form && user.closest("form") !== form)) return null;
            return {user, pass, form};
          }
          function setInputValue(element, value) {
            if (element.value && element.value !== value) return false;
            const setter = Object.getOwnPropertyDescriptor(
                HTMLInputElement.prototype, "value")?.set;
            if (typeof element.focus === "function") element.focus();
            element.dispatchEvent(new FocusEvent("focus", {bubbles:true}));
            if (setter) setter.call(element,value);
            else element.value = value;
            element.dispatchEvent(new Event("input", {bubbles:true}));
            element.dispatchEvent(new Event("change", {bubbles:true}));
            element.dispatchEvent(new FocusEvent("blur", {bubbles:true}));
            return element.value === value;
          }
          function markSubmitted() {
            submitted = true;
            try { sessionStorage.setItem(attemptKey,"submitted"); }
            catch (_) { /* No persistent state; still prevent duplicate on this page. */ }
          }
          function tryFill() {
            if (submitted) return true;
            const found = findLoginFields();
            if (!found) return false;
            const {user,pass,form} = found;
            if (!setInputValue(user,username) || !setInputValue(pass,password))
              return false;

            const area = form || pass.closest('[role="dialog"], main, section') ||
                document;
            const forbidden = element =>
                /kennwort\s*ändern|passwort\s*ändern/i.test(
                    (element.textContent || element.value || "").trim());
            const loginText = element =>
                /^(anmelden|anmeldung|login|log in|sign in|einloggen)$/i.test(
                    (element.textContent || element.value || "").trim());
            const buttons = Array.from(area.querySelectorAll(
                'button[type="submit"],input[type="submit"],button,'+
                'input[type="button"]'));
            const button = buttons.find(el => active(el) && !forbidden(el) &&
                (el.matches('button[type="submit"],input[type="submit"]') ||
                 loginText(el) || /login|submit/i.test(
                    (el.getAttribute("name") || "") + " " +
                    (el.getAttribute("id") || ""))));
            if (form && typeof form.requestSubmit === "function") {
              markSubmitted();
              form.requestSubmit();
              return true;
            }
            if (button) {
              markSubmitted();
              button.click();
              return true;
            }
            // Like WPF, attempt Enter only on the password field if there
            // really is an HTML form. Never click unrelated controls.
            if (form) {
              markSubmitted();
              pass.focus();
              for (const type of ["keydown","keypress","keyup"])
                pass.dispatchEvent(new KeyboardEvent(type,{
                    key:"Enter",code:"Enter",bubbles:true}));
              return true;
            }
            return false;
          }

          // WPF retries at 0/250/750/1500/2500ms. Additional bounded
          // attempts help slower JavaScript apps; never retry a submitted
          // login (prevents account lockouts).
          for (const delay of [0,250,750,1500,2500,5000,8500])
            setTimeout(tryFill,delay);
          if (document.readyState === "loading")
            document.addEventListener("DOMContentLoaded",tryFill,{once:true});
          if (typeof MutationObserver === "function") {
            const observer = new MutationObserver(() => {
              if (tryFill()) observer.disconnect();
            });
            const attach = () => {
              if (document.documentElement)
                observer.observe(document.documentElement,{
                  subtree:true,childList:true
                });
            };
            if (document.documentElement) attach();
            else document.addEventListener("DOMContentLoaded",attach,{once:true});
            setTimeout(()=>observer.disconnect(),10000);
          }
        })();
        </script>
        """;

    internal static string Inject(string html,string username,string password,
        string host,string nonce)
    {
        if(string.IsNullOrWhiteSpace(username)||
           string.IsNullOrEmpty(password)||
           !Regex.IsMatch(host,@"^[a-z0-9-]+(?:\.[a-z0-9-]+)+$",
               RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1))||
           !Regex.IsMatch(nonce,@"^[A-F0-9]{32}$",
               RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1)))
            throw new InvalidOperationException("Invalid dedicated autofill context.");
        if(html.Contains("data-plenaro-dom-autofill",StringComparison.Ordinal))
            return html;
        var script=AutoFillScript
            .Replace("__HOST__",JsonSerializer.Serialize(host),StringComparison.Ordinal)
            .Replace("__USERNAME__",JsonSerializer.Serialize(username),StringComparison.Ordinal)
            .Replace("__PASSWORD__",JsonSerializer.Serialize(password),StringComparison.Ordinal)
            .Replace("__NONCE__",JsonSerializer.Serialize(nonce),StringComparison.Ordinal);
        var head=Regex.Match(html,@"<head\b[^>]*>",RegexOptions.IgnoreCase|
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        if(head.Success)return html.Insert(head.Index+head.Length,script);
        // Some enterprise login documents have no <head> tag.
        var body=Regex.Match(html,@"<body\b[^>]*>",RegexOptions.IgnoreCase|
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        if(body.Success)return html.Insert(body.Index+body.Length,script);
        return html;
    }
}
