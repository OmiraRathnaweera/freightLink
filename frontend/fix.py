import re

with open(r'c:\Users\adees\Desktop\SEF\freightLink\frontend\src\features\agencies\pages\AgencyVerificationPage.jsx', 'r', encoding='utf-8') as f:
    text = f.read()

text = re.sub(r'console\.log\(Approve clicked.*?\)', 'console.log(Approve clicked for agency: )', text)
text = re.sub(r'alert\(Agency.*?approved \(UI only\)\)', 'alert(Agency  approved (UI only))', text)
text = re.sub(r'console\.log\(Suspend clicked.*?\)', 'console.log(Suspend clicked for agency: )', text)
text = re.sub(r'alert\(Agency.*?suspended \(UI only\)\)', 'alert(Agency  suspended (UI only))', text)

with open(r'c:\Users\adees\Desktop\SEF\freightLink\frontend\src\features\agencies\pages\AgencyVerificationPage.jsx', 'w', encoding='utf-8') as f:
    f.write(text)
