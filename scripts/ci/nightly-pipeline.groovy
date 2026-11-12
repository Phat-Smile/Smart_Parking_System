// Nightly run of the integration tests (Testcontainers: disposable PostgreSQL and RabbitMQ) on develop.
// They are too heavy for every pull request on the shared agent, so they run once a night.
def runLogged(String command, String logName) {
    return sh(script: """#!/bin/sh
mkdir -p ci-results/logs
( ${command} ) > ci-results/logs/${logName}.log 2>&1
status=\$?
echo "\$status" > ci-results/logs/${logName}.status
cat ci-results/logs/${logName}.log
exit "\$status"
""", returnStatus: true)
}

def runStrict(String name, String command, String logName) {
    def status = runLogged(command, logName)
    if (status != 0) {
        error("${name} failed (exit ${status}); see ci-results/logs/${logName}.log")
    }
}

def runIntegration(String service) {
    def project = "tests/${service}/${service}.IntegrationTests/${service}.IntegrationTests.csproj"
    def reportDir = "ci-results/dotnet/Integration/${service}"
    def report = "${reportDir}/junit.xml"
    catchError(buildResult: 'FAILURE', stageResult: 'FAILURE', catchInterruptions: false) {
        def status = runLogged(
            "dotnet test --project '${project}' --no-build --no-restore --minimum-expected-tests 1 --results-directory \"\$WORKSPACE/${reportDir}\" -- --report-xunit-junit --report-xunit-junit-filename junit.xml",
            "dotnet-integration-${service.toLowerCase()}"
        )
        if (!fileExists(report)) {
            error("${service} integration tests produced no report (exit ${status}); see ci-results/logs")
        }
        def summary = junit testResults: report, allowEmptyResults: false
        if (summary.failCount > 0 || status != 0) {
            error("${service} integration tests: ${summary.failCount}/${summary.totalCount} failed (exit ${status})")
        }
        echo "PASS: ${service} integration tests: ${summary.totalCount} tests"
    }
}

pipeline {
    agent { label 'parkwise-dotnet10' }

    triggers { cron('H 2 * * *') }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 120, unit: 'MINUTES')
    }

    environment {
        CI = 'true'
    }

    stages {
        stage('Checkout develop') {
            steps {
                checkout scm
                sh 'git log -1 --oneline'
                sh 'rm -rf -- ci-results && mkdir ci-results'
            }
        }
        stage('Docker available') {
            steps { sh 'docker version --format "{{.Server.Version}}"' }
        }
        stage('Backend Restore + Build') {
            steps {
                sh 'dotnet tool restore'
                script {
                    runStrict('Backend Restore', 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers', 'backend-restore')
                    runStrict('Backend Build', 'dotnet build ParkingSystem.slnx --no-restore -m:1 --disable-build-servers', 'backend-build')
                }
            }
        }
        stage('Identity Integration') { steps { script { runIntegration('Identity') } } }
        stage('Parking Integration') { steps { script { runIntegration('Parking') } } }
        stage('Booking Integration') { steps { script { runIntegration('Booking') } } }
        stage('Payment Integration') { steps { script { runIntegration('Payment') } } }
        stage('Notification Integration') { steps { script { runIntegration('Notification') } } }
    }

    post {
        success { echo 'ParkWise nightly integration tests: SUCCESS' }
        failure { echo 'ParkWise nightly integration tests: FAILED; see Test Result and archived ci-results logs.' }
        always { archiveArtifacts artifacts: 'ci-results/**/*', allowEmptyArchive: true }
    }
}
