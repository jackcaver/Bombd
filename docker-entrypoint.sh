#!/bin/sh

# Load the OpenSSL binaries we compiled, there's probably a better way to do this
LD_LIBRARY_PATH=/usr/local/lib:${LD_LIBRARY_PATH}
export LD_LIBRARY_PATH

# Make sure we're in the root folder so we can load assets
cd /bombd/app

# Start the server
exec gosu bombd /bombd/app/Bombd
exit