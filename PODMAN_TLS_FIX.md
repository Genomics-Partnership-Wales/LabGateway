# Podman TLS Certificate Error Fix - Quick Reference

## Problem
When pulling Docker images with Podman, you encounter:
```
Error: initializing source docker://image:latest: pinging container registry registry-1.docker.io: 
Get "https://registry-1.docker.io/v2/": tls: failed to verify certificate: x509: certificate signed by unknown authority
```

This typically occurs behind corporate proxies or firewalls with custom certificates.

---

## Solution: Disable TLS Verification (Quick Fix)

### Step 1: Connect to Podman Machine
```powershell
podman machine ssh
```

### Step 2: Check Current Registry Configuration (Optional)
```bash
cat /etc/containers/registries.conf
```

### Step 3: Add Insecure Registry Configuration
```bash
echo '
[[registry]]
location = "docker.io"
insecure = true

[[registry]]
location = "registry-1.docker.io"
insecure = true
' | sudo tee -a /etc/containers/registries.conf
```

### Step 4: Exit SSH Session
```bash
exit
```

### Step 5: Restart Podman Machine
```powershell
podman machine stop
podman machine start
```

### Step 6: Test the Fix
```powershell
podman pull maildev/maildev:latest
# or any other image
```

---

## What This Does
- Configures Podman to skip TLS certificate verification for Docker Hub
- Allows image pulls to work despite certificate issues
- Changes persist across Podman machine restarts

## Security Note
⚠️ **Warning**: This allows unencrypted/unverified connections to Docker Hub. Only use in development environments or when behind trusted corporate networks.

---

## Alternative: Install Corporate CA Certificates (More Secure)

If you have your corporate CA certificate file:

### Step 1: SSH into Podman Machine
```powershell
podman machine ssh
```

### Step 2: Copy CA Certificate
```bash
# If you have the cert file locally, copy it to the machine first
# From your Windows host (in a separate terminal):
# podman machine ssh -c "cat > /tmp/corporate-ca.crt" < C:\path\to\corporate-ca.crt

# Then in the SSH session:
sudo cp /tmp/corporate-ca.crt /etc/pki/ca-trust/source/anchors/
sudo update-ca-trust
```

### Step 3: Restart Podman Machine
```bash
exit
```
```powershell
podman machine stop
podman machine start
```

---

## Troubleshooting

### Images Still Won't Pull
1. Verify the configuration was added:
   ```bash
   podman machine ssh
   cat /etc/containers/registries.conf | grep -A 2 "docker.io"
   ```

2. Check Podman machine is running:
   ```powershell
   podman machine list
   ```

3. Try pulling with verbose output:
   ```powershell
   podman pull --log-level=debug image-name:tag
   ```

### Revert Changes
To remove the insecure registry configuration:
```bash
podman machine ssh
sudo nano /etc/containers/registries.conf
# Remove the [[registry]] sections for docker.io
# Save and exit (Ctrl+X, Y, Enter)
exit
```
```powershell
podman machine stop
podman machine start
```

---

## Quick Command Reference

| Action | Command |
|--------|---------|
| SSH into Podman machine | `podman machine ssh` |
| Exit SSH | `exit` |
| Stop Podman machine | `podman machine stop` |
| Start Podman machine | `podman machine start` |
| Restart Podman machine | `podman machine stop; podman machine start` |
| View registry config | `podman machine ssh -c "cat /etc/containers/registries.conf"` |
| List running containers | `podman ps` |
| List all containers | `podman ps -a` |
| List images | `podman images` |
| Pull an image | `podman pull image:tag` |

---

## Related Issues
- Corporate proxy with SSL inspection
- Self-signed certificates
- Private registry with untrusted certificates
- WSL2 network configuration issues

**Last Updated**: November 30, 2025
