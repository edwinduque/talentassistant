# NGINX server block template (SPEC-015).
# Place at /etc/nginx/sites-available/talentassistant.conf, enable with a
# symlink at sites-enabled and run certbot for the TLS certificate. This
# block is BRAND-NEW for Talent Assistant: it replaces nothing on the host
# and references no other site's settings.

server {
    listen 80;
    listen [::]:80;
    server_name TALENTASSISTANT_DOMAIN;

    # Certbot rewrites this block and adds its own redirect after issuing.
    location / {
        proxy_pass http://127.0.0.1:5102;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;
    }
}
