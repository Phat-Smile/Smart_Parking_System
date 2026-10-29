// Archive the real runner output without losing its exit status.
def runLogged(String command, String logName) {
    return sh(script: """#!/bin/sh
mkdir -p ci-results/logs
${command} > ci-results/logs/${logName}.log 2>&1
status=\$?
cat ci-results/logs/${logName}.log
exit "\$status"
""", returnStatus: true)
}

def runValidation(String name, String command, String logName) {
    catchError(buildResult: 'FAILURE', stageResult: 'FAILURE', catchInterruptions: false) {
        def status = runLogged(command, logName)
        if (status != 0) {
            error("${name} failed (exit ${status}); see ci-results/logs/${logName}.log")
        }
        echo "PASS: ${name}"
    }
}

def runReportedTest(String name, String command, String report, String logName) {
    catchError(buildResult: 'FAILURE', stageResult: 'FAILURE', catchInterruptions: false) {
        def status = runLogged(command, logName)
        if (!fileExists(report)) {
            error("CI execution/reporting failure in ${name}: missing ${report} (exit ${status}); see ci-results/logs/${logName}.log")
        }
        def summary = junit testResults: report, allowEmptyResults: false
        if (summary.totalCount == 0) {
            error("CI reporting failure in ${name}: ${report} contains no tests")
        }
        if (summary.failCount > 0) {
            error("FAIL: ${name}: ${summary.failCount}/${summary.totalCount} tests failed; see Jenkins Test Result and ci-results/logs/${logName}.log")
        }
        if (status != 0) {
            error("CI test runner failure in ${name}: exit ${status} with no failed tests in ${report}; see ci-results/logs/${logName}.log")
        }
        echo "PASS: ${name}: ${summary.totalCount} tests"
    }
}

def runDotnetTest(String service, String kind, String extra = '') {
    def project = "tests/${service}/${service}.${kind}Tests/${service}.${kind}Tests.csproj"
    if (service == 'Architecture') {
        project = 'tests/ArchitectureTests/ArchitectureTests.csproj'
    } else if (service == 'WebFoundation') {
        project = 'tests/WebFoundationTests/WebFoundationTests.csproj'
    }
    def reportDir = "ci-results/dotnet/${kind}/${service}"
    def command = "dotnet test --project '${project}' --no-build --no-restore --minimum-expected-tests 1 --results-directory \"\$WORKSPACE/${reportDir}\" -- --report-xunit-junit --report-xunit-junit-filename junit.xml ${extra}"
    runReportedTest("${service} ${kind} Tests", command, "${reportDir}/junit.xml", "dotnet-${kind.toLowerCase()}-${service.toLowerCase()}")
}

pipeline {
    agent { label 'parkwise-dotnet10' }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def checkoutInfo = checkout scm
                    env.CHECKED_OUT_BRANCH = checkoutInfo.GIT_BRANCH ?: ''
                    sh 'rm -rf -- ci-results && mkdir ci-results'
                }
            }
        }

        stage('Verify develop branch') {
            steps {
                script {
                    def allowedBranches = ['develop', 'origin/develop', 'refs/heads/develop', 'refs/remotes/origin/develop']
                    if (!allowedBranches.contains(env.CHECKED_OUT_BRANCH)) {
                        error "Expected develop checkout, got '${env.CHECKED_OUT_BRANCH ?: 'unknown'}'"
                    }
                    if (env.BRANCH_NAME && env.BRANCH_NAME != 'develop') {
                        error "Expected develop job, got '${env.BRANCH_NAME}'"
                    }
                }
            }
        }

        stage('Restore') {
            steps {
                sh 'dotnet --version'
                sh 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build ParkingSystem.slnx --no-restore -m:1 --disable-build-servers'
            }
        }

        stage('Identity Unit Tests') { steps { script { runDotnetTest('Identity', 'Unit') } } }
        stage('Parking Unit Tests') { steps { script { runDotnetTest('Parking', 'Unit') } } }
        stage('Booking Unit Tests') { steps { script { runDotnetTest('Booking', 'Unit') } } }
        stage('Payment Unit Tests') { steps { script { runDotnetTest('Payment', 'Unit') } } }
        stage('Notification Unit Tests') { steps { script { runDotnetTest('Notification', 'Unit') } } }
        stage('Architecture Tests') { steps { script { runDotnetTest('Architecture', 'Architecture') } } }
        stage('Web Foundation Tests') { steps { script { runDotnetTest('WebFoundation', 'WebFoundation') } } }

        stage('Identity Integration Tests') {
            environment { TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal' }
            steps { script { runDotnetTest('Identity', 'Integration', '--parallel none') } }
        }
        stage('Parking Integration Tests') {
            environment { TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal' }
            steps { script { runDotnetTest('Parking', 'Integration', '--parallel none') } }
        }
        stage('Booking Integration Tests') {
            environment { TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal' }
            steps { script { runDotnetTest('Booking', 'Integration', '--parallel none') } }
        }
        stage('Payment Integration Tests') {
            environment { TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal' }
            steps { script { runDotnetTest('Payment', 'Integration', '--parallel none') } }
        }
        stage('Notification Integration Tests') {
            environment { TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal' }
            steps { script { runDotnetTest('Notification', 'Integration', '--parallel none') } }
        }

        stage('Frontend Typecheck') {
            agent { label 'parkwise-pr' }
            steps {
                checkout scm
                sh 'rm -rf -- ci-results && mkdir ci-results'
                sh 'node --version'
                sh 'npm --version'
                sh 'npm --prefix web ci'
                script { runValidation('Frontend Typecheck', 'npm --prefix web run typecheck', 'frontend-typecheck') }
            }
            post {
                always { archiveArtifacts artifacts: 'ci-results/**/*', allowEmptyArchive: true }
            }
        }

    }

    post {
        always {
            archiveArtifacts artifacts: 'ci-results/**/*', allowEmptyArchive: true
        }
    }
}
