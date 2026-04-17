# SonarQube Quickstart

## Step 1: Access the web interface

Navigate to http://localhost:9000 in your browser. Default credentials are: admin / admin.

## Step 2: Generate a project token

Go to My Account → Security → Generate Tokens. Create a token for your project analysis:

```bash
# Save your token securely
export SONAR_TOKEN=your_token_here
```

## Step 3: Analyze a project

Run a code analysis using the SonarScanner:

```bash
sonar-scanner \
  -Dsonar.projectKey=my-project \
  -Dsonar.sources=src \
  -Dsonar.host.url=http://localhost:9000 \
  -Dsonar.token=$SONAR_TOKEN
```

## Step 4: Review results

Open your project in SonarQube to view bugs, vulnerabilities, code smells, and coverage metrics. Use the issue navigator to address findings.