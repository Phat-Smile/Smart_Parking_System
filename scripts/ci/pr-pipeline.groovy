// Keep the trusted PR pipeline self-contained; do not load Groovy from the PR checkout.
def runLogged(String command, String logName) {
    return sh(script: """#!/bin/sh
mkdir -p ci-results/logs
# Subshell: a command that starts with "cd" must not move the log path away from the workspace.
( ${command} ) > ci-results/logs/${logName}.log 2>&1
status=\$?
echo "\$status" > ci-results/logs/${logName}.status
cat ci-results/logs/${logName}.log
exit "\$status"
""", returnStatus: true)
}

// Stops the pipeline on failure (like a plain sh step) but keeps the log and exit code for the failure report.
def runStrict(String name, String command, String logName) {
    def status = runLogged(command, logName)
    if (status != 0) {
        error("${name} failed (exit ${status}); see ci-results/logs/${logName}.log")
    }
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

// True when the PR changes a file matching the extended regex (compared with the trusted develop commit).
def changedMatches(String pattern) {
    if (!env.CI_BASE_SHA) {
        return false
    }
    return sh(returnStatus: true, script: "git diff --name-only ${env.CI_BASE_SHA} HEAD | grep -Eq '${pattern}'") == 0
}

// Scripts that run with credentials or gate the PR come from the trusted develop checkout (stashed in Git Policy),
// unpacked into a fresh directory right before use so PR code cannot have edited them.
def withTrustedCi(String dirName, Closure body) {
    dir(dirName) {
        deleteDir()
        unstash 'trusted-ci'
    }
    body()
}

def dockerBuildCheck(String dockerfiles, String logName) {
    runStrict(
        'Docker Build Check',
        "set -e; for f in ${dockerfiles}; do n=\$(echo \"\$f\" | tr '/.' '--' | tr '[:upper:]' '[:lower:]'); echo \"== docker build \$f\"; docker build -q -f \"\$f\" -t \"parkwise-ci/\$n:${env.BUILD_NUMBER}\" .; docker rmi -f \"parkwise-ci/\$n:${env.BUILD_NUMBER}\" > /dev/null; done",
        logName
    )
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
                            env.CI_BASE_SHA = baseSha
                            sh "python3 -m unittest discover -s scripts/ci -p 'test_*.py'"
                            stash name: 'trusted-ci', includes: 'scripts/ci/*.py'

                            dir(candidateDir) {
                                def prRef = "refs/remotes/origin/PR-${env.CHANGE_ID}^{commit}"
                                def headSha = sh(
                                    script: "git rev-parse --verify '${prRef}'",
                                    returnStdout: true
                                ).trim()
                                def checkoutSha = sh(script: 'git rev-parse --verify HEAD', returnStdout: true).trim()
                                sh "git merge-base --is-ancestor ${headSha} ${checkoutSha}"
                                env.CI_HEAD_SHA = headSha
                                withEnv([
                                    "POLICY_CHECKOUT=${policyDir}",
                                    "POLICY_HEAD_BRANCH=${env.CHANGE_BRANCH}",
                                    "POLICY_PR_TITLE=${env.CHANGE_TITLE}",
                                    "POLICY_BASE_SHA=${baseSha}",
                                    "POLICY_HEAD_SHA=${headSha}",
                                    'POLICY_CUTOVER_UTC=2026-10-06T01:41:50Z'
                                ]) {
                                    sh 'git fetch --no-tags "$POLICY_CHECKOUT" "$POLICY_BASE_SHA"'
                                    runStrict('Git Policy', 'python3 "$POLICY_CHECKOUT/scripts/ci/check_git_policy.py"', 'git-policy')
                                    runStrict('Secret Scan', 'python3 "$POLICY_CHECKOUT/scripts/ci/check_secrets.py"', 'secret-scan')
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
                script {
                    runStrict('Backend Restore', 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers', 'backend-restore')
                    runStrict('Backend Build', 'dotnet build ParkingSystem.slnx --no-restore -m:1 --disable-build-servers', 'backend-build')
                }
            }
        }

        stage('Docker Build (.NET services)') {
            when { expression { changedMatches('^(src/.*Dockerfile|contracts/|Directory\\.(Packages|Build)\\.|global\\.json)') } }
            steps {
                script {
                    dockerBuildCheck(
                        'src/Services/Identity/Identity.WebAPI/Dockerfile src/Services/Parking/Parking.WebAPI/Dockerfile src/Services/Booking/Booking.WebAPI/Dockerfile src/Services/Payment/Payment.WebAPI/Dockerfile src/Services/Notification/Notification.WebAPI/Dockerfile src/Gateway/Dockerfile',
                        'docker-build-dotnet'
                    )
                }
            }
        }

        // Backend checks and the web / Python checks run side by side on the agent (two branches, not more: the agent shares RAM with the demo stack).
        stage('Checks') {
            parallel {
                stage('Backend') {
                    stages {
                        stage('Migration Drift') {
                            steps {
                                script {
                                    withTrustedCi('.ci-trusted') {
                                        runStrict('Migration Drift', 'python3 .ci-trusted/scripts/ci/check_migrations.py', 'migration-drift')
                                    }
                                }
                            }
                        }
                        stage('Identity Unit Tests') { steps { script { runDotnetTest('Identity', 'Unit') } } }
                        stage('Parking Unit Tests') { steps { script { runDotnetTest('Parking', 'Unit') } } }
                        stage('Booking Unit Tests') { steps { script { runDotnetTest('Booking', 'Unit') } } }
                        stage('Payment Unit Tests') { steps { script { runDotnetTest('Payment', 'Unit') } } }
                        stage('Notification Unit Tests') { steps { script { runDotnetTest('Notification', 'Unit') } } }
                        stage('Architecture Tests') { steps { script { runDotnetTest('Architecture', 'Architecture') } } }
                        stage('Web Foundation Tests') { steps { script { runDotnetTest('WebFoundation', 'WebFoundation') } } }
                    }
                }

                stage('Web and Python') {
                    stages {
                        stage('Frontend Dependencies') {
                            steps {
                                sh 'node --version'
                                sh 'npm --version'
                                script { runStrict('Frontend Dependencies', 'npm --prefix web ci', 'frontend-install') }
                                sh 'rm -rf -- web/.next'
                            }
                        }
                        stage('Frontend Lint') { steps { script { runValidation('Frontend Lint', 'npm --prefix web run lint', 'frontend-lint') } } }
                        stage('Frontend Typecheck') { steps { script { runValidation('Frontend Typecheck', 'npm --prefix web run typecheck', 'frontend-typecheck') } } }
                        stage('Frontend Format') { steps { script { runValidation('Frontend Format', 'npm --prefix web run format:check', 'frontend-format') } } }
                        stage('Frontend Vitest') {
                            steps {
                                script {
                                    // verbose prints "(retry xN)" for a test that failed once and passed on retry, which the report lists as flaky.
                                    runReportedTest(
                                        'Frontend Vitest',
                                        'npm --prefix web run test:run -- --reporter=verbose --reporter=junit --outputFile.junit=../ci-results/frontend/vitest/junit.xml --retry=1',
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
                                script { runStrict('Python AI Dependencies', 'ai/ai-service/.ci-venv/bin/python -m pip install --disable-pip-version-check -r ai/ai-service/requirements.lock', 'ai-deps') }
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
                                        'cd ai/ai-service && PYTHONPATH=. .ci-venv/bin/python ../../scripts/ci/unittest_junit.py --start-directory tests --output ../../ci-results/ai/junit.xml',
                                        'ci-results/ai/junit.xml',
                                        'ai-unittest'
                                    )
                                }
                            }
                        }
                        stage('IoT Simulator Tests') {
                            steps {
                                script {
                                    runValidation('IoT Simulator Tests', 'python3 -m unittest discover -s edge/iot-simulator/src -p "test_*.py" -v', 'iot-simulator')
                                }
                            }
                        }
                    }
                }
            }
        }

        stage('Docker Build (Web and AI)') {
            when { expression { changedMatches('^(web/Dockerfile|ai/ai-service/Dockerfile)$') } }
            steps {
                script { dockerBuildCheck('web/Dockerfile ai/ai-service/Dockerfile', 'docker-build-web-ai') }
            }
        }
    }

    post {
        success { echo 'ParkWise PR CI: SUCCESS' }
        failure { echo 'ParkWise PR CI: FAILED; see the failure report above, Test Result and archived ci-results logs.' }
        always {
            script {
                boolean trusted = false
                try {
                    dir('.ci-trusted-post') {
                        deleteDir()
                        unstash 'trusted-ci'
                    }
                    trusted = true
                } catch (Exception ignored) {
                    echo 'Trusted CI scripts are not available (the build stopped before Git Policy); skipping the failure report.'
                }
                if (trusted) {
                    sh 'mkdir -p ci-results && python3 .ci-trusted-post/scripts/ci/failure_report.py ci-results --output ci-results/failure-report.md --text ci-results/failure-summary.txt --only-if-findings || true'
                    if (fileExists('ci-results/failure-summary.txt')) {
                        currentBuild.description = readFile('ci-results/failure-summary.txt')
                    }
                    if (fileExists('ci-results/failure-report.md')) {
                        echo readFile('ci-results/failure-report.md')
                    }
                    try {
                        withCredentials([usernamePassword(credentialsId: scm.userRemoteConfigs[0].credentialsId, usernameVariable: 'GH_USER', passwordVariable: 'GH_TOKEN')]) {
                            withEnv([
                                "CI_REPO_URL=${scm.userRemoteConfigs[0].url}",
                                "CI_RESULT=${currentBuild.currentResult}",
                                "CI_SHA=${env.CI_HEAD_SHA ?: ''}"
                            ]) {
                                sh 'python3 .ci-trusted-post/scripts/ci/post_pr_comment.py --repo-url "$CI_REPO_URL" --pr "$CHANGE_ID" --status "$CI_RESULT" --build-url "$BUILD_URL" --sha "$CI_SHA" --report ci-results/failure-report.md || true'
                            }
                        }
                    } catch (Exception commentError) {
                        echo "Could not post the PR comment: ${commentError.message}"
                    }
                }
            }
            archiveArtifacts artifacts: 'ci-results/**/*', allowEmptyArchive: true
        }
    }
}
