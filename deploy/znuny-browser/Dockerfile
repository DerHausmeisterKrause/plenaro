FROM selenium/standalone-chromium:4.51.0-20261010

# Preserve the upstream Selenium user, bundled Chromium and health behavior.
# CA installation occurs at startup from a read-only Docker bind mount,
# so no company certificates or secrets end up inside an image layer.
COPY --chown=seluser:seluser deploy/znuny-browser/entrypoint.sh /opt/plenaro/entrypoint.sh
USER seluser
ENTRYPOINT ["/bin/bash", "/opt/plenaro/entrypoint.sh"]
