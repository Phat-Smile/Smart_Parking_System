// Keep the trusted PR pipeline self-contained; do not load Groovy from the PR checkout.
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
    agent { label 'parkwise-pr' }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 60, unit: 'MINUTES')
    }

    environment {
        CI = 'true'
    }

    stages {
        stage('Verify Pull Request') {
            steps {
                script {
                    if (!(env.CHANGE_ID ==~ /[0-9]+/)) {
                        error('A numeric CHANGE_ID is required for this Pull Request pipeline.')
                    }
                    if (env.CHANGE_TARGET != 'develop') {
                        error("Expected PR targeting develop, got '${env.CHANGE_TARGET ?: 'unknown'}'")
                    }
                    if (!env.CHANGE_BRANCH?.trim() || !env.CHANGE_TITLE?.trim()) {
                        error('CHANGE_BRANCH and CHANGE_TITLE are required for Git Policy validation.')
                    }
                    echo "PR Number: ${env.CHANGE_ID}"
                    echo "PR Source: ${env.CHANGE_BRANCH}"
                    echo "PR Target: ${env.CHANGE_TARGET}"
                }
            }
        }

        stage('Checkout PR') {
            steps {
                checkout scm
                sh 'git log -1 --oneline'
                sh 'rm -rf -- ci-results && mkdir ci-results'
            }
        }

        stage('Git Policy') {
            steps {
                script {
                    def candidateDir = pwd()
                    String repoUrl = scm.userRemoteConfigs[0].url
                    String credentialId = scm.userRemoteConfigs[0].credentialsId
                    if (!repoUrl || !credentialId) {
                        error('GitHub SCM URL and checkout credential are required for trusted policy checkout.')
                    }

                    ws("${candidateDir}@trusted-policy") {
                        deleteDir()
                        try {
                            checkout([
                                $class: 'GitSCM',
                                branches: [[name: '*/develop']],
                                userRemoteConfigs: [[
                                    url: repoUrl,
                                    credentialsId: credentialId,
                                    refspec: '+refs/heads/develop:refs/remotes/origin/develop'
                                ]],
                                extensions: [[$class: 'CloneOption', noTags: true, shallow: false]]
                            ])
                            def policyDir = pwd()
                            def baseSha = sh(script: 'git rev-parse --verify HEAD', returnStdout: true).trim()
                            sh "python3 -m unittest discover -s scripts/ci -p 'test_*.py'"

                            dir(candidateDir) {
                                def prRef = "refs/remotes/origin/PR-${env.CHANGE_ID}^{commit}"
                                def headSha = sh(
                                    script: "git rev-parse --verify '${prRef}'",
                                    returnStdout: true
                                ).trim()
                                def checkoutSha = sh(script: 'git rev-parse --verify HEAD', returnStdout: true).trim()
                                sh "git merge-base --is-ancestor ${headSha} ${checkoutSha}"
                                withEnv([
                                    "POLICY_CHECKOUT=${policyDir}",
                                    "POLICY_HEAD_BRANCH=${env.CHANGE_BRANCH}",
                                    "POLICY_PR_TITLE=${env.CHANGE_TITLE}",
                                    "POLICY_BASE_SHA=${baseSha}",
                                    "POLICY_HEAD_SHA=${headSha}",
                                    'POLICY_CUTOVER_UTC=2026-10-06T01:41:50Z'
                                ]) {
                                    sh 'git fetch --no-tags "$POLICY_CHECKOUT" "$POLICY_BASE_SHA"'
                                    sh 'python3 "$POLICY_CHECKOUT/scripts/ci/check_git_policy.py"'
                                }
                            }
                        } finally {
                            deleteDir()
                        }
                    }
                }
            }
        }

        stage('Backend Restore + Build') {
            steps {
                sh 'dotnet --version'
                sh 'dotnet tool restore'
                sh 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers'
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

        stage('Frontend Dependencies') {
            steps {
                sh 'node --version'
                sh 'npm --version'
                sh 'npm --prefix web ci'
                sh 'rm -rf -- web/.next'
            }
        }
        stage('Frontend Lint') { steps { script { runValidation('Frontend Lint', 'npm --prefix web run lint', 'frontend-lint') } } }
        stage('Frontend Typecheck') { steps { script { runValidation('Frontend Typecheck', 'npm --prefix web run typecheck', 'frontend-typecheck') } } }
        stage('Frontend Format') { steps { script { runValidation('Frontend Format', 'npm --prefix web run format:check', 'frontend-format') } } }
        stage('Frontend Vitest') {
            steps {
                script {
                    runReportedTest(
                        'Frontend Vitest',
                        'npm --prefix web run test:run -- --reporter=default --reporter=junit --outputFile.junit=../ci-results/frontend/vitest/junit.xml',
                        'ci-results/frontend/vitest/junit.xml',
                        'frontend-vitest'
                    )
                }
            }
        }
        stage('Frontend Build') { steps { script { runValidation('Frontend Build', 'npm --prefix web run build', 'frontend-build') } } }

        stage('Python AI Dependencies') {
            steps {
                sh 'python3 --version'
                sh 'python3 -m venv --clear ai/ai-service/.ci-venv'
                sh 'ai/ai-service/.ci-venv/bin/python -m pip install --disable-pip-version-check -r ai/ai-service/requirements.lock'
            }
        }
        stage('Python AI Compile') {
            steps {
                script {
                    runValidation('Python AI Compile', 'cd ai/ai-service && .ci-venv/bin/python -m compileall -q app tests', 'ai-compile')
                }
            }
        }
        stage('Python AI Tests') {
            steps {
                script {
                    runReportedTest(
                        'Python AI Tests',
                        'cd ai/ai-service && .ci-venv/bin/python ../../scripts/ci/unittest_junit.py --start-directory tests --output ../../ci-results/ai/junit.xml',
                        'ci-results/ai/junit.xml',
                        'ai-unittest'
                    )
                }
            }
        }

    }

    post {
        success { echo 'ParkWise PR CI: SUCCESS' }
        failure { echo 'ParkWise PR CI: FAILED; review the failed stage, Test Result and archived ci-results logs.' }
        always { archiveArtifacts artifacts: 'ci-results/**/*', allowEmptyArchive: true }
    }
}
