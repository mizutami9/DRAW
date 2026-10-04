import requests, sys
login = sys.argv[1]
q = 'query{user(login:"%s"){id displayName followers{totalCount}}}' % login
for cid in ['kd1unb4b3q4t58fwlpcbzcbnm76a8fp', 'ue6666qo983tsx6so1t0vnawi233wa', 'jzkbprff40iqj646a697cyrvl0zt2m6']:
    r = requests.post('https://gql.twitch.tv/gql', json={'query': q}, headers={'Client-ID': cid}, timeout=20)
    print(cid, r.status_code, r.text[:300])
