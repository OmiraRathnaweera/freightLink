import urllib.request
import json
import time

backend_url = "http://localhost:5159/api/v1"

def req(url, data=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = f'Bearer {token}'
    r = urllib.request.Request(url, data=json.dumps(data).encode('utf-8') if data else None, headers=headers)
    with urllib.request.urlopen(r) as resp:
        return json.loads(resp.read().decode('utf-8'))

# 1. Register a shipper
shipper_email = f"testshipper_{int(time.time())}@example.com"
print("Registering shipper", shipper_email)
try:
    reg_resp = req(f"{backend_url}/auth/register/shipper", {
        "email": shipper_email,
        "password": "Password123!",
        "companyName": "Test Shipper",
        "contactNumber": "1234567890",
        "fullName": "Test User",
        "billingAddress": "123 Test St"
    })
except Exception as e:
    print("Reg err", e.read())

# 2. Login
print("Logging in")
login_resp = req(f"{backend_url}/auth/login", {
    "email": shipper_email,
    "password": "Password123!"
})
token = login_resp["accessToken"]

# 3. Create a load
print("Creating load")
try:
    load_resp = req(f"{backend_url}/loads", {
        "cargoDescription": "Pallets",
        "weightKg": 500,
        "volumeM3": 3.2,
        "pickupAddress": "123 Pickup St",
        "pickupLat": 34.0,
        "pickupLng": -118.0,
        "dropoffAddress": "456 Dropoff Ave",
        "dropoffLat": 35.0,
        "dropoffLng": -119.0,
        "pickupWindowStart": "2027-01-01T00:00:00Z",
        "pickupWindowEnd": "2027-01-02T00:00:00Z"
    }, token)
except Exception as e:
    print("Load err", e.read())

load_id = load_resp["id"] if "id" in load_resp else load_resp.get("loadId")
shipper_user_id = load_resp.get("shipperUserId")

# 4. Trigger the Python Agent
print(f"Triggering agent with load {load_id}")
agent_url = "http://127.0.0.1:8001/workflows/run"
headers = {
    'Content-Type': 'application/json',
    'X-Internal-Api-Key': 'my-local-test-secret'
}
data = {
    'loadId': load_id,
    'triggeredByUserId': shipper_user_id,
    'attemptNo': 1,
    'loadContext': {
    'weightKg': 500,
    'volumeM3': 3.2,
    'cargoDescription': 'Pallets',
    'pickupLat': 34.0,
    'pickupLng': -118.0
    },
    'candidateAgencies': [
    {
        'id': '33333333-3333-3333-3333-333333333333',
        'name': 'Alpha Trucking (Passes All)',
        'status': 'Active',
        'yardLat': 34.0,
        'yardLng': -118.0,
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
r = urllib.request.Request(agent_url, data=json.dumps(data).encode('utf-8'), headers=headers, method='POST')
try:
    with urllib.request.urlopen(r) as response:
        print('Agent Status:', response.status)
        print('Agent Response:', response.read().decode('utf-8'))
except Exception as e:
    print('Agent Error:', str(e))
    if hasattr(e, 'read'):
        print('Agent Body:', e.read().decode('utf-8'))
