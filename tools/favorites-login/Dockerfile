FROM mcr.microsoft.com/playwright:v1.64.1-noble
WORKDIR /app
# Keep the JS driver in lockstep with Chromium already in the official image.
RUN npm install --no-audit --no-fund --omit=dev playwright@1.64.1
COPY tools/favorites-login/server.mjs /app/server.mjs
COPY tools/favorites-login/worker.test.mjs /app/worker.test.mjs
COPY tools/favorites-login/entrypoint.sh /app/entrypoint.sh
EXPOSE 3030
ENTRYPOINT ["/bin/sh","/app/entrypoint.sh"]
