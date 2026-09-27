import psycopg2
import json
import urllib.request
import time

def get_load():
    conn = psycopg2.connect(host="localhost", port=5432, dbname="freightlink", user="postgres", password="PostgreSQL.2026")
    cur = conn.cursor()
    cur.execute('SELECT "LoadId", "ShipperUserId" FROM "Loads" LIMIT 1')
    row = cur.fetchone()
    if row:
        return row[0], row[1]
    return None, None

load_id, shipper_id = get_load()

if not load_id:
    print("No loads found in DB! We need to create one first.")
else:
    print(f"Found load {load_id} for shipper {shipper_id}")
    url = 'http://127.0.0.1:8001/workflows/run'
    headers = {
        'Content-Type': 'application/json',
        'X-Internal-Api-Key': 'my-local-test-secret'
    }
    data = {
      'loadId': str(load_id),
      'triggeredByUserId': str(shipper_id),
      'attemptNo': 1,
      'loadContext': {
        'weightKg': 500,
        'volumeM3': 3.2,
        'cargoDescription': 'Palletized dry goods',
        'pickupLat': 34.0522,
        'pickupLng': -118.2437
      },
      'candidateAgencies': [
        {
          'id': '33333333-3333-3333-3333-333333333333',
          'name': 'Alpha Trucking (Passes All)',
          'status': 'Active',
          'yardLat': 34.0622,
          'yardLng': -118.2537,
          'compliance': {
            'id': '88888888-8888-8888-8888-888888888888',
            'expiryDate': '2030-12-31'
          },
          'fleet': [
            {
              'id': '77777777-7777-7777-7777-777777777777',
              'name': 'Large Van',
              'maxPayloadKg': 1000,
              'maxVolumeM3': 10.0
            }
          ]
        }
      ]
    }
    req = urllib.request.Request(url, data=json.dumps(data).encode('utf-8'), headers=headers, method='POST')
    try:
        with urllib.request.urlopen(req) as response:
            print('Status:', response.status)
            print('Response:', response.read().decode('utf-8'))
    except Exception as e:
        print('Error:', str(e))
        if hasattr(e, 'read'):
            print('Body:', e.read().decode('utf-8'))
